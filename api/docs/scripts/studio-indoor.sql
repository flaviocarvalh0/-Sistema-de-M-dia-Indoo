-- =========================================================================
-- Mídia Indoor — configuração completa do Atlas Studio
-- =========================================================================
-- Deixa o Studio no estado que o painel (web/) espera:
--
--   1. Módulos "Rede" (/rede, da operadora) e "Mídia Indoor" (/indoor, do
--      parceiro), com ícone, cor e ordem no launcher.
--   2. Entidades parceiro, tela, midia e playlist: rótulo, route_key (o
--      segmento da URL no web/app.routes.ts), ícone, histórico e módulo.
--   3. Menus de cada módulo, com ícone e ordem.
--   4. Grupos "Operadora" e "Proprietário de Parceiro".
--   5. RBAC: as permissões de cada grupo, por módulo e entidade.
--
-- Rotas resultantes no painel:
--   /rede/parceiro   /rede/tela   /rede/playlist   (operadora)
--   /indoor/midia    /indoor/tela                  (parceiro)
--
-- Papéis (ADR-014 e ADR-015): a operadora é dona das TVs e monta as grades
-- (playlists) com as mídias dos parceiros. O parceiro é anunciante: cuida da
-- biblioteca de mídias dele (dentro do plano) e vê "onde meus anúncios
-- passam" (/indoor/tela, somente leitura). A entidade "tela" fica no módulo
-- Rede, com menu também no módulo do parceiro.
--
-- Idempotente e convergente: pode rodar quantas vezes quiser; cria o que
-- falta e corrige o que divergir (rótulo, ícone, módulo, ordem). Não apaga
-- nada que tenha sido criado à mão no Studio.
--
-- Pré-requisitos: migrations aplicadas e a API iniciada ao menos uma vez
-- depois delas — os inicializadores registram as entidades em
-- platform.entity_metadata e carregam os ícones (Bootstrap Icons) em
-- platform.system_icon.
--
-- Rodar (Windows):
--   .\docs\scripts\aplicar-script.ps1 .\docs\scripts\studio-indoor.sql
-- ou direto:
--   psql -h localhost -p 5432 -U postgres -d Indoor -v ON_ERROR_STOP=1 -f docs/scripts/studio-indoor.sql
--
-- Os nomes dos grupos são a chave usada pelo código
-- (Atlas.Domain.Business.Constants.BusinessRoles). Não renomeie só aqui.
-- O dono de cada parceiro entra no grupo "Proprietário de Parceiro"
-- automaticamente quando a operadora cadastra o parceiro; usuários da
-- operadora são colocados no grupo "Operadora" pelo Studio (Usuários).
-- =========================================================================

SET client_encoding = 'UTF8';

BEGIN;

DO $$
DECLARE
    v_rede_id         bigint;
    v_indoor_id       bigint;
    v_operadora_id    bigint;
    v_proprietario_id bigint;
    v_faltando        text;
BEGIN
    -- 0. Pré-requisitos ---------------------------------------------------
    SELECT string_agg(t, ', ') INTO v_faltando
      FROM unnest(ARRAY['parceiro', 'tela', 'midia', 'playlist']) AS t
     WHERE NOT EXISTS (SELECT 1 FROM platform.entity_metadata em WHERE em.table_name = t);

    IF v_faltando IS NOT NULL THEN
        RAISE EXCEPTION 'Entidades ausentes em platform.entity_metadata: %. Aplique as migrations e inicie a API uma vez antes de rodar este script.', v_faltando;
    END IF;

    SELECT string_agg(i, ', ') INTO v_faltando
      FROM unnest(ARRAY['broadcast', 'tv', 'people', 'display', 'collection-play', 'list-ol']) AS i
     WHERE NOT EXISTS (SELECT 1 FROM platform.system_icon si WHERE si.name = i);

    IF v_faltando IS NOT NULL THEN
        RAISE EXCEPTION 'Ícones ausentes em platform.system_icon: %. Inicie a API uma vez (o IconInitializer carrega os ícones) antes de rodar este script.', v_faltando;
    END IF;

    -- 1. Módulos ---------------------------------------------------------
    -- A chave é o route_prefix: é o que o web/app.routes.ts registra.
    SELECT id INTO v_rede_id FROM platform.system_module WHERE route_prefix = '/rede';

    IF v_rede_id IS NULL THEN
        INSERT INTO platform.system_module
            (name, description, route_prefix, sort_order, system_icon_id, hex_color,
             created, created_by, last_modified, last_modified_by)
        VALUES
            ('Rede', '', '/rede', 0, NULL, '', now(), NULL, now(), NULL)
        RETURNING id INTO v_rede_id;
    END IF;

    UPDATE platform.system_module
       SET name           = 'Rede',
           description    = 'Cadastro e gestão dos parceiros',
           sort_order     = 10,
           system_icon_id = (SELECT id FROM platform.system_icon WHERE name = 'people'),
           hex_color      = '#1B4256',
           last_modified  = now()
     WHERE id = v_rede_id;

    SELECT id INTO v_indoor_id FROM platform.system_module WHERE route_prefix = '/indoor';

    IF v_indoor_id IS NULL THEN
        INSERT INTO platform.system_module
            (name, description, route_prefix, sort_order, system_icon_id, hex_color,
             created, created_by, last_modified, last_modified_by)
        VALUES
            ('Mídia Indoor', '', '/indoor', 0, NULL, '', now(), NULL, now(), NULL)
        RETURNING id INTO v_indoor_id;
    END IF;

    UPDATE platform.system_module
       SET name           = 'Mídia Indoor',
           description    = 'Telas, mídias e playlists do parceiro',
           sort_order     = 20,
           system_icon_id = (SELECT id FROM platform.system_icon WHERE name = 'broadcast'),
           hex_color      = '#0F766E',
           last_modified  = now()
     WHERE id = v_indoor_id;

    -- 2. Entidades -------------------------------------------------------
    -- route_key = segmento da URL do painel (/<route_prefix>/<route_key>).
    UPDATE platform.entity_metadata em
       SET display_label    = e.rotulo,
           route_key        = e.route_key,
           system_icon_id   = (SELECT id FROM platform.system_icon WHERE name = e.icone),
           system_module_id = e.modulo_id,
           enable_history   = true,
           last_modified    = now()
      FROM (VALUES
            ('parceiro', 'Parceiros', 'parceiro', 'people',          v_rede_id),
            ('tela',     'Telas',     'tela',     'tv',              v_rede_id),
            ('midia',    'Mídias',    'midia',    'collection-play', v_indoor_id),
            ('playlist', 'Playlists', 'playlist', 'list-ol',         v_rede_id)
           ) AS e(table_name, rotulo, route_key, icone, modulo_id)
     WHERE em.table_name = e.table_name;

    -- 3. Menus (itens de entidade, sem agrupador) ------------------------
    -- Cria os que faltam...
    INSERT INTO platform.system_menu_item
        (display_label, sort_order, menu_type, system_module_id, parent_id,
         system_icon_id, entity_metadata_id, icon_hex_color,
         created, created_by, last_modified, last_modified_by)
    SELECT m.rotulo, m.ordem, 2 /* SystemMenuType.Entity */, m.modulo_id, NULL,
           NULL, em.id, NULL,
           now(), NULL, now(), NULL
      FROM (VALUES
            ('parceiro', 'Parceiros',                 1, v_rede_id),
            ('tela',     'Telas',                     2, v_rede_id),
            ('playlist', 'Playlists',                 3, v_rede_id),
            ('midia',    'Mídias',                    1, v_indoor_id),
            ('tela',     'Onde meus anúncios passam', 2, v_indoor_id)
           ) AS m(table_name, rotulo, ordem, modulo_id)
      JOIN platform.entity_metadata em ON em.table_name = m.table_name
     WHERE NOT EXISTS (
           SELECT 1 FROM platform.system_menu_item mi
            WHERE mi.system_module_id = m.modulo_id
              AND mi.entity_metadata_id = em.id);

    -- ...e alinha rótulo, ordem e ícone de todos.
    UPDATE platform.system_menu_item mi
       SET display_label  = m.rotulo,
           sort_order     = m.ordem,
           system_icon_id = (SELECT id FROM platform.system_icon WHERE name = m.icone),
           icon_hex_color = NULL,
           last_modified  = now()
      FROM (VALUES
            ('parceiro', 'Parceiros',                 1, 'people',          v_rede_id),
            ('tela',     'Telas',                     2, 'tv',              v_rede_id),
            ('playlist', 'Playlists',                 3, 'list-ol',         v_rede_id),
            ('midia',    'Mídias',                    1, 'collection-play', v_indoor_id),
            ('tela',     'Onde meus anúncios passam', 2, 'broadcast',       v_indoor_id)
           ) AS m(table_name, rotulo, ordem, icone, modulo_id)
      JOIN platform.entity_metadata em ON em.table_name = m.table_name
     WHERE mi.system_module_id = m.modulo_id
       AND mi.entity_metadata_id = em.id;

    -- 4. Grupos ------------------------------------------------------------
    -- normalized_name segue o UpperInvariant do Identity.
    INSERT INTO platform.role (name, normalized_name, concurrency_stamp, is_active, created_at, created_by)
    SELECT g.name, g.normalized_name, gen_random_uuid()::text, true, now(), NULL
      FROM (VALUES
            ('Operadora',               'OPERADORA'),
            ('Proprietário de Parceiro', 'PROPRIETÁRIO DE PARCEIRO')
           ) AS g(name, normalized_name)
     WHERE NOT EXISTS (SELECT 1 FROM platform.role r WHERE r.normalized_name = g.normalized_name);

    UPDATE platform.role SET is_active = true
     WHERE normalized_name IN ('OPERADORA', 'PROPRIETÁRIO DE PARCEIRO')
       AND is_active IS DISTINCT FROM true;

    SELECT id INTO v_operadora_id    FROM platform.role WHERE normalized_name = 'OPERADORA';
    SELECT id INTO v_proprietario_id FROM platform.role WHERE normalized_name = 'PROPRIETÁRIO DE PARCEIRO';

    -- 5. RBAC --------------------------------------------------------------
    -- Operadora: parceiros (sem Delete: o parceiro é bloqueado, nunca
    -- apagado), telas (o hardware é dela) e playlists (as grades da rede;
    -- Playlists.View também libera a leitura das mídias de todos os
    -- parceiros, para montar as grades).
    -- Proprietário de Parceiro (anunciante): tudo nas próprias mídias e
    -- leitura de "onde meus anúncios passam" (Telas.View). O isolamento por
    -- parceiro é feito pela API, não pela permissão.
    -- module_id/entity_name ligam a permissão ao módulo no launcher e ao
    -- RBAC do front (canView/canCreate/... por nome da tabela).
    UPDATE platform.role_claim rc
       SET module_id = p.module_id,
           entity_name = p.entity_name
      FROM (VALUES
            (v_operadora_id,    'Parceiros.View',   v_rede_id,   'parceiro'),
            (v_operadora_id,    'Parceiros.Create', v_rede_id,   'parceiro'),
            (v_operadora_id,    'Parceiros.Update', v_rede_id,   'parceiro'),
            (v_operadora_id,    'Telas.View',       v_rede_id,   'tela'),
            (v_operadora_id,    'Telas.Create',     v_rede_id,   'tela'),
            (v_operadora_id,    'Telas.Update',     v_rede_id,   'tela'),
            (v_operadora_id,    'Telas.Delete',     v_rede_id,   'tela'),
            (v_operadora_id,    'Playlists.View',   v_rede_id,   'playlist'),
            (v_operadora_id,    'Playlists.Create', v_rede_id,   'playlist'),
            (v_operadora_id,    'Playlists.Update', v_rede_id,   'playlist'),
            (v_operadora_id,    'Playlists.Delete', v_rede_id,   'playlist'),
            (v_proprietario_id, 'Telas.View',       v_indoor_id, 'tela'),
            (v_proprietario_id, 'Midias.View',      v_indoor_id, 'midia'),
            (v_proprietario_id, 'Midias.Create',    v_indoor_id, 'midia'),
            (v_proprietario_id, 'Midias.Update',    v_indoor_id, 'midia'),
            (v_proprietario_id, 'Midias.Delete',    v_indoor_id, 'midia')
           ) AS p(role_id, claim_value, module_id, entity_name)
     WHERE rc.role_id = p.role_id
       AND rc.claim_type = 'permission'
       AND rc.claim_value = p.claim_value
       AND (rc.module_id IS DISTINCT FROM p.module_id OR rc.entity_name IS DISTINCT FROM p.entity_name);

    INSERT INTO platform.role_claim (role_id, claim_type, claim_value, module_id, entity_name)
    SELECT p.role_id, 'permission', p.claim_value, p.module_id, p.entity_name
      FROM (VALUES
            (v_operadora_id,    'Parceiros.View',   v_rede_id,   'parceiro'),
            (v_operadora_id,    'Parceiros.Create', v_rede_id,   'parceiro'),
            (v_operadora_id,    'Parceiros.Update', v_rede_id,   'parceiro'),
            (v_operadora_id,    'Telas.View',       v_rede_id,   'tela'),
            (v_operadora_id,    'Telas.Create',     v_rede_id,   'tela'),
            (v_operadora_id,    'Telas.Update',     v_rede_id,   'tela'),
            (v_operadora_id,    'Telas.Delete',     v_rede_id,   'tela'),
            (v_operadora_id,    'Playlists.View',   v_rede_id,   'playlist'),
            (v_operadora_id,    'Playlists.Create', v_rede_id,   'playlist'),
            (v_operadora_id,    'Playlists.Update', v_rede_id,   'playlist'),
            (v_operadora_id,    'Playlists.Delete', v_rede_id,   'playlist'),
            (v_proprietario_id, 'Telas.View',       v_indoor_id, 'tela'),
            (v_proprietario_id, 'Midias.View',      v_indoor_id, 'midia'),
            (v_proprietario_id, 'Midias.Create',    v_indoor_id, 'midia'),
            (v_proprietario_id, 'Midias.Update',    v_indoor_id, 'midia'),
            (v_proprietario_id, 'Midias.Delete',    v_indoor_id, 'midia')
           ) AS p(role_id, claim_value, module_id, entity_name)
     WHERE NOT EXISTS (
           SELECT 1 FROM platform.role_claim rc
            WHERE rc.role_id = p.role_id
              AND rc.claim_type = 'permission'
              AND rc.claim_value = p.claim_value);

    -- O parceiro não altera telas nem playlists (versões anteriores deste
    -- script davam).
    DELETE FROM platform.role_claim
     WHERE role_id = v_proprietario_id
       AND claim_type = 'permission'
       AND claim_value IN ('Telas.Create', 'Telas.Update', 'Telas.Delete',
                           'Playlists.View', 'Playlists.Create', 'Playlists.Update', 'Playlists.Delete');

    -- O menu de playlists saiu do módulo do parceiro.
    DELETE FROM platform.system_menu_item mi
     USING platform.entity_metadata em
     WHERE mi.entity_metadata_id = em.id
       AND em.table_name = 'playlist'
       AND mi.system_module_id = v_indoor_id;

    RAISE NOTICE 'Módulos: Rede (%) e Mídia Indoor (%). Grupos: Operadora (%) e Proprietário de Parceiro (%).',
        v_rede_id, v_indoor_id, v_operadora_id, v_proprietario_id;
END
$$;

COMMIT;

-- Conferência -------------------------------------------------------------
SELECT m.name AS modulo, m.route_prefix || '/' || em.route_key AS rota,
       mi.display_label AS menu, mi.sort_order AS ordem, si.name AS icone
  FROM platform.system_menu_item mi
  JOIN platform.system_module m ON m.id = mi.system_module_id
  JOIN platform.entity_metadata em ON em.id = mi.entity_metadata_id
  LEFT JOIN platform.system_icon si ON si.id = mi.system_icon_id
 WHERE m.route_prefix IN ('/rede', '/indoor')
 ORDER BY m.sort_order, mi.sort_order;

SELECT r.name AS grupo, string_agg(rc.claim_value, ', ' ORDER BY rc.claim_value) AS permissoes
  FROM platform.role r
  JOIN platform.role_claim rc ON rc.role_id = r.id AND rc.claim_type = 'permission'
 WHERE r.normalized_name IN ('OPERADORA', 'PROPRIETÁRIO DE PARCEIRO')
 GROUP BY r.name
 ORDER BY r.name;

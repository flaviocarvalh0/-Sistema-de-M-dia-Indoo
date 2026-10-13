-- =========================================================================
-- Mídia Indoor — configuração do Atlas Studio
-- =========================================================================
-- Cria os módulos "Rede" (/rede, da operadora) e "Mídia Indoor" (/indoor,
-- do parceiro), vincula as entidades, monta os menus e cadastra os grupos
-- "Operadora" e "Proprietário de Parceiro" com as permissões.
--
-- Idempotente: pode rodar de novo sem duplicar nada. Equivale ao que se faria
-- pelas telas do Studio; existe para o ambiente novo nascer igual ao de
-- desenvolvimento.
--
-- Pré-requisito: migrations aplicadas e API iniciada ao menos uma vez (o
-- inicializador registra as entidades em platform.entity_metadata).
--
-- Os nomes dos grupos são a chave usada pelo código
-- (Atlas.Domain.Business.Constants.BusinessRoles). Não renomeie só aqui.
-- =========================================================================

SET client_encoding = 'UTF8';

BEGIN;

DO $$
DECLARE
    v_rede_id         bigint;
    v_indoor_id       bigint;
    v_operadora_id    bigint;
    v_proprietario_id bigint;
BEGIN
    IF (SELECT count(*) FROM platform.entity_metadata
        WHERE table_name IN ('parceiro', 'tela', 'midia', 'playlist')) <> 4 THEN
        RAISE EXCEPTION 'Entidades da Mídia Indoor ausentes em platform.entity_metadata. Inicie a API uma vez antes de rodar este script.';
    END IF;

    -- 1. Módulos ---------------------------------------------------------
    SELECT id INTO v_rede_id FROM platform.system_module WHERE route_prefix = '/rede';

    IF v_rede_id IS NULL THEN
        INSERT INTO platform.system_module
            (name, description, route_prefix, sort_order, system_icon_id, hex_color,
             created, created_by, last_modified, last_modified_by)
        VALUES
            ('Rede', 'Cadastro e gestão dos parceiros', '/rede', 10, NULL, '#1B2A41',
             now(), NULL, now(), NULL)
        RETURNING id INTO v_rede_id;
    END IF;

    SELECT id INTO v_indoor_id FROM platform.system_module WHERE route_prefix = '/indoor';

    IF v_indoor_id IS NULL THEN
        INSERT INTO platform.system_module
            (name, description, route_prefix, sort_order, system_icon_id, hex_color,
             created, created_by, last_modified, last_modified_by)
        VALUES
            ('Mídia Indoor', 'Telas, mídias e playlists do parceiro', '/indoor', 20, NULL, '#0F766E',
             now(), NULL, now(), NULL)
        RETURNING id INTO v_indoor_id;
    END IF;

    -- 2. Entidades nos módulos -------------------------------------------
    UPDATE platform.entity_metadata
       SET system_module_id = v_rede_id
     WHERE table_name = 'parceiro'
       AND system_module_id IS DISTINCT FROM v_rede_id;

    UPDATE platform.entity_metadata
       SET system_module_id = v_indoor_id
     WHERE table_name IN ('tela', 'midia', 'playlist')
       AND system_module_id IS DISTINCT FROM v_indoor_id;

    -- 3. Menus (itens de entidade, sem agrupador) ------------------------
    INSERT INTO platform.system_menu_item
        (display_label, sort_order, menu_type, system_module_id, parent_id,
         system_icon_id, entity_metadata_id, icon_hex_color,
         created, created_by, last_modified, last_modified_by)
    SELECT em.display_label, ordem.sort_order, 2 /* SystemMenuType.Entity */, ordem.modulo_id, NULL,
           NULL, em.id, NULL,
           now(), NULL, now(), NULL
      FROM (VALUES
            ('parceiro', 1, v_rede_id),
            ('tela',     1, v_indoor_id),
            ('midia',    2, v_indoor_id),
            ('playlist', 3, v_indoor_id)
           ) AS ordem(table_name, sort_order, modulo_id)
      JOIN platform.entity_metadata em ON em.table_name = ordem.table_name
     WHERE NOT EXISTS (
           SELECT 1 FROM platform.system_menu_item mi
            WHERE mi.system_module_id = ordem.modulo_id
              AND mi.entity_metadata_id = em.id);

    -- 4. Grupos ------------------------------------------------------------
    -- normalized_name segue o UpperInvariant do Identity.
    INSERT INTO platform.role (name, normalized_name, concurrency_stamp, is_active, created_at, created_by)
    SELECT g.name, g.normalized_name, gen_random_uuid()::text, true, now(), NULL
      FROM (VALUES
            ('Operadora',               'OPERADORA'),
            ('Proprietário de Parceiro', 'PROPRIETÁRIO DE PARCEIRO')
           ) AS g(name, normalized_name)
     WHERE NOT EXISTS (SELECT 1 FROM platform.role r WHERE r.normalized_name = g.normalized_name);

    SELECT id INTO v_operadora_id    FROM platform.role WHERE normalized_name = 'OPERADORA';
    SELECT id INTO v_proprietario_id FROM platform.role WHERE normalized_name = 'PROPRIETÁRIO DE PARCEIRO';

    -- 5. Permissões ------------------------------------------------------
    -- Operadora: cadastro e gestão dos parceiros. Delete não tem uso (o
    -- parceiro é bloqueado, nunca apagado).
    -- Proprietário de Parceiro: tudo nas próprias telas, mídias e playlists
    -- (o isolamento por parceiro é do servidor, não da permissão).
    INSERT INTO platform.role_claim (role_id, claim_type, claim_value, module_id, entity_name)
    SELECT p.role_id, 'permission', p.claim_value, p.module_id, p.entity_name
      FROM (VALUES
            (v_operadora_id,    'Parceiros.View',   v_rede_id,   'parceiro'),
            (v_operadora_id,    'Parceiros.Create', v_rede_id,   'parceiro'),
            (v_operadora_id,    'Parceiros.Update', v_rede_id,   'parceiro'),
            (v_proprietario_id, 'Telas.View',       v_indoor_id, 'tela'),
            (v_proprietario_id, 'Telas.Create',     v_indoor_id, 'tela'),
            (v_proprietario_id, 'Telas.Update',     v_indoor_id, 'tela'),
            (v_proprietario_id, 'Telas.Delete',     v_indoor_id, 'tela'),
            (v_proprietario_id, 'Midias.View',      v_indoor_id, 'midia'),
            (v_proprietario_id, 'Midias.Create',    v_indoor_id, 'midia'),
            (v_proprietario_id, 'Midias.Update',    v_indoor_id, 'midia'),
            (v_proprietario_id, 'Midias.Delete',    v_indoor_id, 'midia'),
            (v_proprietario_id, 'Playlists.View',   v_indoor_id, 'playlist'),
            (v_proprietario_id, 'Playlists.Create', v_indoor_id, 'playlist'),
            (v_proprietario_id, 'Playlists.Update', v_indoor_id, 'playlist'),
            (v_proprietario_id, 'Playlists.Delete', v_indoor_id, 'playlist')
           ) AS p(role_id, claim_value, module_id, entity_name)
     WHERE NOT EXISTS (
           SELECT 1 FROM platform.role_claim rc
            WHERE rc.role_id = p.role_id
              AND rc.claim_type = 'permission'
              AND rc.claim_value = p.claim_value);

    RAISE NOTICE 'Módulos Rede % e Mídia Indoor %; grupos Operadora % e Proprietário de Parceiro %.',
        v_rede_id, v_indoor_id, v_operadora_id, v_proprietario_id;
END
$$;

COMMIT;

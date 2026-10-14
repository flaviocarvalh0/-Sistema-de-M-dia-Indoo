# Catálogo de funcionalidades — Mídia Indoor

O que o produto faz hoje, por quem usa. Recursos herdados da plataforma
(login, usuários, grupos, menus, SMTP, histórico) estão em
`api/docs/platform/Funcionalidades-da-Plataforma.md`.

## Quem usa

| Perfil | Onde | O quê |
|---|---|---|
| **Operadora** (grupo "Operadora") | Painel → Rede | Cadastra os parceiros e as TVs (o hardware é dela): instala, pareia e libera cada TV para um parceiro |
| **Parceiro** (grupo "Proprietário de Parceiro") | Painel → Mídia Indoor | Envia mídias, monta playlists e escolhe o que cada TV dele toca |
| **TV** | App `player/` | Toca a grade do parceiro, mesmo sem internet |
| **Administrador** (`admin`) | Atlas Studio | Usuários, grupos, menus, SMTP e parâmetros |

## Rede (operadora)

| Funcionalidade | Detalhe |
|---|---|
| Cadastro de parceiro | Nome, responsável, e-mail de acesso, telefone, CPF/CNPJ, cidade, fuso horário e espaço (MB) |
| Acesso do responsável | O cadastro cria o usuário do responsável e envia a senha temporária por e-mail; a troca é obrigatória no primeiro login |
| Espaço por parceiro | Cota de armazenamento em MB (padrão 200), conferida antes de cada envio |
| Fuso horário | Define o "hoje" e o dia da semana da grade das TVs do parceiro |
| Bloquear / desbloquear | Com motivo. Bloqueado, o parceiro perde o painel e as TVs param de receber grade nova, mas continuam tocando o que já têm |
| Lista | Situação, telas, espaço usado/cota; filtro por situação; histórico de alterações |

## Mídias (parceiro)

| Funcionalidade | Detalhe |
|---|---|
| Envio | Vídeo MP4 ou imagem JPG/PNG de até 50 MB, direto para o storage, com barra de progresso |
| Conferência do arquivo | SHA-256 calculado no navegador e conferido pela API; tipo real verificado (um PNG disfarçado de MP4 é recusado) |
| Duração | Vídeo: lida do próprio arquivo. Imagem: 15 s por padrão. Editável |
| Período de exibição | "Exibir a partir de" e "Exibir até" (opcionais) |
| Ativar / inativar | Inativa sai do loop das TVs, mas continua nas playlists |
| Pré-visualizar | Link temporário de 1 h |
| Excluir | Só mídia fora de playlists; o arquivo sai do storage |
| Espaço usado | Barra com uso e cota do parceiro |

## Playlists (parceiro)

| Funcionalidade | Detalhe |
|---|---|
| Playlist reutilizável | A mesma playlist toca em várias telas |
| Grade | Adicionar mídias, reordenar arrastando, remover |
| Dias da semana por item | Botões Seg…Dom e atalhos (todos os dias, seg a sex, fim de semana) |
| Ciclo | Duração total do loop e quantas telas usam a playlist |
| Duplicar | "Clonar" cria outra playlist com a mesma grade |
| Ativar / inativar | Inativa: as telas dela ficam sem grade |
| Concorrência | Salvar com a versão: edição simultânea não sobrescreve a do outro (409) |

## Telas — operadora (Rede → Telas)

O hardware é da operadora: ela cadastra cada TV e a libera para um parceiro.

| Funcionalidade | Detalhe |
|---|---|
| Cadastro | Parceiro, nome, local e identificação do aparelho (MAC/série, só informativa) |
| Lista da rede | Todas as TVs, com parceiro, playlist e conexão; filtro por parceiro e situação; busca por nome, parceiro, local ou aparelho |
| Código de pareamento | 8 caracteres, válido por 24 h, uso único; exibido em destaque com contagem regressiva |
| Conexão | Aguardando pareamento, Online ou Offline (sem sincronizar há 5 min); a lista atualiza a cada minuto |
| Parear outra TV | Gera um código novo; a TV atual continua até a nova parear |
| Desvincular TV | A TV perde o acesso na próxima sincronização e a tela volta a aguardar pareamento |
| Trocar de parceiro | A playlist sai (era do parceiro anterior), a TV é desvinculada e o código pendente descartado |
| Ativar / inativar | Inativa: a TV continua no que já baixou, sem receber grade nova |
| Copiar | "Clonar" cria outra tela no mesmo parceiro e local |

## Telas — parceiro (Mídia Indoor → Telas)

| Funcionalidade | Detalhe |
|---|---|
| Minhas TVs | Somente leitura: nome, local, playlist, conexão e última sincronização |
| Trocar playlist | Escolhe a playlist de cada TV (vale na próxima sincronização) |

## App das TVs

| Funcionalidade | Detalhe |
|---|---|
| Pareamento | Código digitado no controle (aceita minúsculas e hífen) |
| Offline-first | Só toca arquivos baixados; sem internet, continua tocando |
| Download seguro | Conferência do SHA-256, 3 tentativas, arquivos reaproveitados entre grades |
| Troca sem tela preta | A grade nova só entra quando tudo baixou |
| Agenda | Dias da semana e período calculados na TV, no fuso do parceiro, a cada item |
| Loop | Vídeo até o fim, imagem pela duração; arquivo com problema é pulado |
| Tela de espera | Sem nada para hoje, mostra o nome da tela |
| Informações | Botão OK do controle: conexão, última sincronização, itens de hoje, servidor e versão |
| Sempre no ar | Tela cheia, sempre acesa, abre sozinho ao ligar a TV |
| Revogação | Desvinculada, continua tocando e pede novo pareamento |

## Segurança e isolamento

- Cada parceiro só vê e altera os próprios registros. Registro de outro
  parceiro responde 404.
- As TVs usam um token próprio, que não abre o painel (ADR-011).
- O bucket das mídias é privado, acessado por links temporários (ADR-012).
- O pareamento tem limite de 10 tentativas por IP a cada 5 minutos. A
  sincronização tem limite de 30 por minuto por tela.
- Histórico de alterações de parceiros, telas, mídias e playlists.

## Fora do escopo atual

- Autocadastro de parceiros (hoje só a operadora cadastra).
- Equipe do parceiro com permissões diferentes do dono (o papel "Equipe"
  existe no modelo, sem tela).
- Operadora acessar o painel de um parceiro ("acessar como").
- Relatório de exibições (prova de veiculação).
- Agendamento por horário do dia (hoje: dia da semana e período).

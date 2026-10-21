# Catálogo de funcionalidades — Mídia Indoor

O que o produto faz hoje, por quem usa. Recursos herdados da plataforma
(login, usuários, grupos, menus, SMTP, histórico) estão em
`api/docs/platform/Funcionalidades-da-Plataforma.md`.

## Quem usa

| Perfil | Onde | O quê |
|---|---|---|
| **Operadora** (grupo "Operadora") | Painel → Rede | Cadastra parceiros, clientes e TVs (o hardware é dela), monta as playlists com os anúncios da rede e escolhe o que cada TV toca |
| **Cliente** (grupo "Cliente") | Painel → Mídia Indoor | Anunciante: envia e mantém as próprias mídias, dentro do plano, e vê onde elas passam |
| **Parceiro** | — | O local onde a TV fica (academia, clínica). **Não acessa o sistema**; se também anuncia, é cadastrado como cliente (ADR-022) |
| **TV** | App `player/` | Toca a grade da tela com notícias, relógio e clima, mesmo sem internet |
| **Administrador** (`admin`) | Atlas Studio | Usuários, grupos, menus, SMTP e parâmetros |

## Parceiros (operadora, Rede → Parceiros)

| Funcionalidade | Detalhe |
|---|---|
| Cadastro | Nome do local, responsável, telefone, e-mail de contato, CPF/CNPJ, endereço, cidade (clima das TVs) e fuso horário (o "hoje" e o dia da semana da grade das TVs daqui). Sem login |
| Também anuncia como | Ligação opcional ao cadastro de cliente do dono: no diretório das TVs do local, ele aparece primeiro. Um cliente pode ter várias unidades |
| Ativar / inativar | Inativo sai da lista ao cadastrar telas; as TVs dele seguem como estão |
| Excluir | Só sem telas no local |
| Lista | Cidade, telas no local, com quem está ligado, situação; filtro por situação; histórico de alterações |

## Clientes (operadora, Rede → Clientes)

| Funcionalidade | Detalhe |
|---|---|
| Cadastro de cliente | Nome, responsável, e-mail de acesso, telefone, CPF/CNPJ, cidade, fuso horário (o "hoje" do painel dele) e espaço (MB) |
| Acesso do responsável | O cadastro cria o usuário do responsável e envia a senha temporária por e-mail; a troca é obrigatória no primeiro login |
| Espaço por cliente | Cota de armazenamento em MB (padrão 200), conferida antes de cada envio |
| Plano (mídias ativas) | Quantas mídias o cliente pode ter ativas ao mesmo tempo (padrão 1). Reduzir não inativa nada: só impede ativar novas |
| Bloquear / desbloquear | Com motivo. Bloqueado, o cliente perde o painel e as mídias dele saem das grades na próxima sincronização |
| Lista | Situação, TVs com anúncio dele, mídias ativas (x de y), espaço usado/cota; filtro por situação; histórico de alterações |
| Armazenamento da rede | Barra acima da lista: usado, em envio e o teto (no máximo os 10 GB gratuitos do R2), última medição do bucket e arquivos sem dono apagados. E-mail à operadora a 70%, 85% e 95%. Acima do teto, novos envios são recusados (ADR-025) |

## Mídias (cliente)

| Funcionalidade | Detalhe |
|---|---|
| Envio | Vídeo MP4 ou imagem JPG/PNG de até 50 MB, direto para o storage, com barra de progresso |
| Conferência do arquivo | SHA-256 calculado no navegador e conferido pela API; tipo real verificado (um PNG disfarçado de MP4 é recusado) |
| Duração | Vídeo: lida do próprio arquivo. Imagem: 15 s por padrão. Editável |
| Período de exibição | "Exibir a partir de" e "Exibir até" (opcionais) |
| Ativar / inativar | Inativa sai do loop das TVs, mas continua nas grades. Mídia nova nasce ativa se o plano tiver vaga e inativa se estiver completo; ativar além do plano é recusado com a explicação |
| Plano | "Mídias ativas: x de y" na lista, com aviso de plano completo também na página de envio |
| Substituir arquivo | Troca o vídeo/imagem de uma mídia sem mudar a situação. Fora das grades, troca na hora. **Em grade, vai para a aprovação da operadora** (ADR-020): as TVs seguem com o atual até aprovar; a lista mostra "Aguardando aprovação" ou o motivo da recusa, que também chega por e-mail. Aprovada, as TVs baixam o novo pelo hash e o antigo sai do storage |
| QR code | Opcional, na aba "QR code" da edição. O formulário pede só o que o tipo precisa: **Link** (endereço), **WhatsApp** (telefone e mensagem inicial), **Instagram** (usuário), **Telefone** (número) ou **Pix** (chave, nome e cidade de quem recebe e valor opcional — vira um Pix copia e cola do Banco Central). Legenda, canto da tela e tamanho escolhidos pelo cliente. "Ver QR code" mostra a prévia para testar com o celular. Link, WhatsApp e Instagram passam pelo link rastreado, que conta as leituras |
| Diretório das TVs | Cartão no topo de Mídias: **Aparecer no diretório** (aceite do dono, desligado por padrão) com categoria, descrição, WhatsApp, Instagram e site. A página mostra só esses contatos (os QR codes dos anúncios não entram) |
| Pré-visualizar | Link temporário de 1 h |
| Excluir | Só mídia fora das grades; o arquivo sai do storage |
| Espaço usado | Barra com uso e cota do cliente |

## Playlists (operadora, Rede → Playlists)

| Funcionalidade | Detalhe |
|---|---|
| Playlist reutilizável | A mesma playlist toca em várias telas; nome único na rede |
| Grade | Adicionar mídias ativas de qualquer cliente (busca por título ou cliente, filtro por cliente), reordenar arrastando, remover |
| Repetição | A mesma mídia pode entrar mais de uma vez; o seletor mostra "Já adicionado (N×)" |
| Anunciante | Cada item mostra o cliente dono da mídia |
| Dias da semana por item | Botões Seg…Dom e atalhos (todos os dias, seg a sex, fim de semana) |
| Ciclo | Duração total do loop e quantas telas usam a playlist |
| Duplicar | "Clonar" cria outra playlist com a mesma grade (mídias inativas ficam de fora) |
| Ativar / inativar | Inativa: as telas dela ficam sem grade |
| Concorrência | Salvar com a versão: edição simultânea não sobrescreve a do outro (409) |
| Informativos | "Um informativo a cada N anúncios" (0 = nenhum) e o tempo de cada um na tela (5 a 60 s). Rodízio: notícia, tempo, notícia, cotações |
| Cartões | "Previsão do tempo" (agora e os próximos 3 dias, da cidade do estabelecimento) e "Cotações" (dólar e euro, PTAX do Banco Central, com a variação do dia) |
| Trocas para aprovar | Cartão no topo de Rede → Playlists com as trocas de arquivo pendentes: **Comparar** (atual e novo lado a lado), **Aprovar** ou **Recusar** com motivo. Aviso por e-mail a cada troca enviada |
| Embaralhar | Sorteia a ordem dos anúncios a cada volta, na TV; itens com 📌 (posição fixa) ficam no lugar |
| Relógio e clima | Liga ou desliga o relógio e a temperatura na tela |
| Tamanho da volta | Segundos de anúncios por volta (ex.: 180). Com mais anúncios do que cabem, a TV roda o carrossel com cursor; a grade mostra em quantas voltas todos passam. 0 = todos em toda volta |

## Fontes de notícias (operadora, Rede → Fontes de notícias)

| Funcionalidade | Detalhe |
|---|---|
| Cadastro | Nome, endereço do feed (RSS ou Atom) e crédito exibido na TV. A Agência Brasil (CC BY) já vem cadastrada |
| Coleta | A API lê as fontes ativas a cada hora e guarda as 30 notícias mais recentes de cada uma, com a imagem no storage |
| Fotos | Do feed ou, sem ela, a foto de destaque da página da notícia (JPEG, PNG ou WebP, até 5 MB). Sem foto, tenta de novo por 24 h |
| Sem repetição | A mesma manchete em duas fontes (ex.: feed geral e de editoria) vai uma vez só para as TVs |
| Coletar agora | Lê o feed na hora (para conferir uma fonte nova) |
| Situação da leitura | Ok, com erro (com a mensagem) ou aguardando; quando foi a última leitura |
| Ver notícias | Título, data e miniatura de cada notícia; as com mais de 48 h aparecem como fora das TVs |
| Excluir | Apaga a fonte, as notícias e as imagens |

## Telas — operadora (Rede → Telas)

O hardware é da operadora: ela instala cada TV num parceiro (o local) e
escolhe a playlist.

| Funcionalidade | Detalhe |
|---|---|
| Cadastro | Parceiro (o local; define o fuso e a cidade do clima), playlist, nome, local e identificação do aparelho (MAC/série, só informativa) |
| Lista da rede | Todas as TVs, com parceiro, playlist e conexão; filtro por parceiro e situação; busca por nome, parceiro, local ou aparelho |
| Código de pareamento | 8 caracteres, válido por 24 h, uso único; exibido em destaque com contagem regressiva |
| Conexão | Aguardando pareamento, Online ou Offline (sem sincronizar há 5 min); a lista atualiza a cada minuto |
| Fora de exibição | Sincroniza mas não exibe há 10 min: app fora da tela, vídeo travado ou "aguardando" com anúncios para hoje. Coluna **Atenção** com o motivo e o aviso de relógio errado (mais de 5 min de diferença). E-mail à operadora, como na queda (ADR-024) |
| Parear outra TV | Gera um código novo; a TV atual continua até a nova parear |
| Desvincular TV | A TV perde o acesso na próxima sincronização e a tela volta a aguardar pareamento |
| Trocar parceiro ou playlist | Vale na próxima sincronização; a TV continua pareada |
| Ativar / inativar | Inativa: a TV continua no que já baixou, sem receber grade nova |
| Copiar | "Clonar" cria outra tela no mesmo parceiro e local |
| Alerta de TV fora do ar | E-mail para a operadora quando uma TV ativa fica 15 min sem sincronizar, só entre 7 h e 22 h no fuso do estabelecimento, e outro quando ela volta. Um resumo por varredura (5 min), sem repetir a mesma queda (ADR-020) |
| Versões do app | **Versões do app** (botão na lista): envio do APK, liberação **Só TVs de teste** → **Todas as TVs**, suspensão e exclusão; quantas TVs estão em cada versão. Coluna **App** na lista e **TV de teste** no cadastro (ADR-021) |
| Diretório de anunciantes | Liga na edição da tela: a TV mostra um QR code fixo que abre uma página pública com os anunciantes da grade de hoje que aceitaram aparecer; o cliente ligado ao parceiro (se ele também anuncia) vem primeiro. **Diretório de anunciantes** na linha mostra o QR (para testar ou imprimir), abre a página e permite **Trocar endereço** (o anterior para de funcionar). O quadro mostra os últimos 30 dias: acessos, pessoas por dia, toques nos contatos e em "Quer anunciar aqui?" e toques por anunciante |

## Onde meus anúncios passam (cliente)

| Funcionalidade | Detalhe |
|---|---|
| Lista | Somente leitura: as TVs ativas cuja playlist tem uma mídia ativa do cliente, com nome, local, parceiro, conexão e última sincronização |

## Relatório de veiculação (cliente em Mídias; operadora na lista de Clientes)

| Funcionalidade | Detalhe |
|---|---|
| Exibições | O que as TVs registraram que exibiram: total, concluídas, interrompidas e quantas foram com a TV sem internet (tocou do que já tinha), com o tempo na tela (ADR-023) |
| Leituras do QR | QR de link, WhatsApp e Instagram passam por um link curto da API que conta a leitura e redireciona. Leituras e leitores únicos por dia, sem guardar IP. Telefone e Pix vão direto (não são contados) |
| Toques no diretório | Toques nos contatos do anunciante (WhatsApp, Instagram, site) na página que o QR da TV abre, por contato e por dia, com pessoas diferentes por dia (ADR-027) |
| Quebras | Por horário (hora local de cada TV, com o horário de pico), por TV, por anúncio e por dia |
| Período | Este mês, mês passado, últimos 7 dias ou datas livres (até 93 dias) |
| Resumo para o WhatsApp | Copia um texto curto com exibições, leituras, toques no diretório e horário de pico para mandar ao anunciante |
| Aviso | O relatório ainda não sabe se a TV estava ligada e na entrada certa |

## App das TVs

| Funcionalidade | Detalhe |
|---|---|
| Pareamento | Código digitado no controle (aceita minúsculas e hífen) |
| Offline-first | Só toca arquivos baixados; sem internet, continua tocando |
| Download seguro | Conferência do SHA-256, 3 tentativas, arquivos reaproveitados entre grades |
| Troca sem tela preta | A grade nova só entra quando tudo baixou |
| Agenda | Dias da semana e período calculados na TV, no fuso do estabelecimento, a cada volta |
| Loop | Vídeo até o fim, imagem pela duração; arquivo com problema é pulado |
| Composição na TV | A cada volta: ordem sorteada (se a playlist pedir, com os itens fixos no lugar) e uma notícia a cada N anúncios, em rodízio |
| Carrossel com cursor | Com "Tamanho da volta" na playlist: cada volta mostra os próximos anúncios de uma fila circular e todos passam uma vez antes de qualquer um repetir (mesma frequência para todos), offline. Os fixados entram em toda volta |
| Notícias | Foto em tela cheia, título animado e crédito da fonte; sem foto, fundo da marca. Mais de 48 h, sai da tela mesmo offline |
| Relógio e clima | No canto da tela: hora do estabelecimento e temperatura da cidade dele (Open-Meteo, com crédito). O clima some depois de 6 h sem atualizar |
| Cartões | Previsão do tempo com ícones e cotações com seta de alta ou baixa, em tela cheia, com o crédito da fonte. Funcionam offline enquanto o dado valer (tempo 6 h, cotação 4 dias) |
| QR code | Por cima do anúncio, no canto escolhido, num cartão branco com a legenda; gerado na TV, funciona offline. O médio ocupa 30% da altura da tela |
| Prova de veiculação | Cada anúncio que sai da tela é gravado na TV (mídia, início, duração, se foi até o fim, se estava sem internet) e enviado em lotes a cada 15 min; sem internet, fica guardado até voltar |
| QR do diretório | Com o diretório ligado na tela: QR pequeno no canto inferior direito, nos anúncios sem QR próprio e na tela de espera. Com o WhatsApp da operadora cadastrado (`INDOOR.ANUNCIE.WHATSAPP`), o cartão convida "Quer anunciar aqui?" e a página tem o botão **Clique aqui e fale com a gente** |
| Visual do QR | Cartão escuro com o QR num quadro branco (a margem que o leitor precisa) e a legenda em branco, para não brigar com o anúncio |
| Tela de espera | Sem nada para hoje, mostra o nome da tela |
| Informações | Botão OK do controle: conexão, última sincronização, itens de hoje, notícias, clima, servidor, versão e estado da atualização |
| Atualização automática | Baixa a versão nova liberada (hash conferido), instala e reabre sozinha. Sem confirmação com o app como device owner; senão pede "Instalar" no controle. Falhou, tenta de novo em 1 h |
| Sempre no ar | Tela cheia, sempre acesa, abre sozinho ao ligar a TV |
| Revogação | Desvinculada, continua tocando e pede novo pareamento |

## Segurança e isolamento

- Cada cliente só vê e altera as próprias mídias. Mídia de outro cliente,
  ou tela onde ele não passa, responde 404.
- Playlists e mídias da rede só para a operadora (ADR-015).
- As TVs usam um token próprio, que não abre o painel (ADR-011).
- O bucket das mídias é privado, acessado por links temporários (ADR-012).
- O pareamento tem limite de 10 tentativas por IP a cada 5 minutos. A
  sincronização tem limite de 30 por minuto por tela.
- Histórico de alterações de parceiros, clientes, telas, mídias e playlists.
- Diretório das TVs (ADR-018): página pública sem login, mas com código
  aleatório por tela (não o id), limite de 60 acessos por minuto por IP e
  cache de 60 s. Mostra só quem aceitou e só os dados do perfil público e
  dos QR codes; fora dos buscadores (`noindex`) e sem dizer ao site do
  anunciante de qual TV veio a visita.
- QR rastreado (ADR-023): `/q/{codigo}` anônimo, código aleatório de 8
  caracteres, 30 leituras por minuto por IP, sem cache e sem `Referer`. A
  leitura guarda só um hash diário (não reversível) para contar únicos; o
  envio de exibições aceita no máximo 500 por lote e 20 lotes por minuto por tela.

## Fora do escopo atual

- Autocadastro de clientes (hoje só a operadora cadastra).
- Equipe do cliente com permissões diferentes do dono (o papel "Equipe"
  existe no modelo, sem tela).
- Operadora acessar o painel de um cliente ("acessar como").
- Saber se a TV estava ligada e na entrada certa (HDMI-CEC ou captura de tela).
- Envio automático do relatório ao anunciante (hoje: copiar o resumo no painel).
- Contagem de leituras do QR de telefone e Pix.
- Diretório: o logo do anunciante no cartão (fase 2 da Entrega 5).
- Fontes de notícias por playlist (hoje as fontes ativas valem para toda a rede).
- Agendamento por horário do dia (hoje: dia da semana e período).

# Catálogo de funcionalidades — Mídia Indoor

O que o produto faz hoje, por quem usa. Recursos herdados da plataforma
(login, usuários, grupos, menus, SMTP, histórico) estão em
`api/docs/platform/Funcionalidades-da-Plataforma.md`.

## Quem usa

| Perfil | Onde | O quê |
|---|---|---|
| **Operadora** (grupo "Operadora") | Painel → Rede | Cadastra os parceiros e as TVs (o hardware é dela), monta as playlists com os anúncios da rede e escolhe o que cada TV toca |
| **Parceiro** (grupo "Proprietário de Parceiro") | Painel → Mídia Indoor | Anunciante: envia e mantém as próprias mídias, dentro do plano, e vê onde elas passam |
| **TV** | App `player/` | Toca a grade da tela com notícias, relógio e clima, mesmo sem internet |
| **Administrador** (`admin`) | Atlas Studio | Usuários, grupos, menus, SMTP e parâmetros |

## Rede (operadora)

| Funcionalidade | Detalhe |
|---|---|
| Cadastro de parceiro | Nome, responsável, e-mail de acesso, telefone, CPF/CNPJ, cidade, fuso horário e espaço (MB) |
| Acesso do responsável | O cadastro cria o usuário do responsável e envia a senha temporária por e-mail; a troca é obrigatória no primeiro login |
| Espaço por parceiro | Cota de armazenamento em MB (padrão 200), conferida antes de cada envio |
| Plano (mídias ativas) | Quantas mídias o parceiro pode ter ativas ao mesmo tempo (padrão 1). Reduzir não inativa nada: só impede ativar novas |
| Fuso horário | Define o "hoje" e o dia da semana da grade das TVs instaladas no parceiro (host) |
| Bloquear / desbloquear | Com motivo. Bloqueado, o parceiro perde o painel e as mídias dele saem das grades na próxima sincronização. As TVs instaladas nele continuam tocando normalmente |
| Lista | Situação, telas no local, mídias ativas (x de y), espaço usado/cota; filtro por situação; histórico de alterações |

## Mídias (parceiro)

| Funcionalidade | Detalhe |
|---|---|
| Envio | Vídeo MP4 ou imagem JPG/PNG de até 50 MB, direto para o storage, com barra de progresso |
| Conferência do arquivo | SHA-256 calculado no navegador e conferido pela API; tipo real verificado (um PNG disfarçado de MP4 é recusado) |
| Duração | Vídeo: lida do próprio arquivo. Imagem: 15 s por padrão. Editável |
| Período de exibição | "Exibir a partir de" e "Exibir até" (opcionais) |
| Ativar / inativar | Inativa sai do loop das TVs, mas continua nas grades. Mídia nova nasce ativa se o plano tiver vaga e inativa se estiver completo; ativar além do plano é recusado com a explicação |
| Plano | "Mídias ativas: x de y" na lista, com aviso de plano completo também na página de envio |
| Substituir arquivo | Troca o vídeo/imagem de uma mídia sem tirá-la das grades nem mudar a situação; as TVs baixam o novo pelo hash. O arquivo antigo sai do storage |
| QR code | Opcional, na aba "QR code" da edição. O formulário pede só o que o tipo precisa: **Link** (endereço), **WhatsApp** (telefone e mensagem inicial), **Instagram** (usuário), **Telefone** (número) ou **Pix** (chave, nome e cidade de quem recebe e valor opcional — vira um Pix copia e cola do Banco Central). Legenda, canto da tela e tamanho escolhidos pelo parceiro. "Ver QR code" mostra a prévia para testar com o celular |
| Pré-visualizar | Link temporário de 1 h |
| Excluir | Só mídia fora das grades; o arquivo sai do storage |
| Espaço usado | Barra com uso e cota do parceiro |

## Playlists (operadora, Rede → Playlists)

| Funcionalidade | Detalhe |
|---|---|
| Playlist reutilizável | A mesma playlist toca em várias telas; nome único na rede |
| Grade | Adicionar mídias ativas de qualquer parceiro (busca por título ou parceiro, filtro por parceiro), reordenar arrastando, remover |
| Repetição | A mesma mídia pode entrar mais de uma vez; o seletor mostra "Já adicionado (N×)" |
| Anunciante | Cada item mostra o parceiro dono da mídia |
| Dias da semana por item | Botões Seg…Dom e atalhos (todos os dias, seg a sex, fim de semana) |
| Ciclo | Duração total do loop e quantas telas usam a playlist |
| Duplicar | "Clonar" cria outra playlist com a mesma grade (mídias inativas ficam de fora) |
| Ativar / inativar | Inativa: as telas dela ficam sem grade |
| Concorrência | Salvar com a versão: edição simultânea não sobrescreve a do outro (409) |
| Informativos | "Um informativo a cada N anúncios" (0 = nenhum) e o tempo de cada um na tela (5 a 60 s). Rodízio: notícia, tempo, notícia, cotações |
| Cartões | "Previsão do tempo" (agora e os próximos 3 dias, da cidade do estabelecimento) e "Cotações" (dólar e euro, PTAX do Banco Central, com a variação do dia) |
| Embaralhar | Sorteia a ordem dos anúncios a cada volta, na TV; itens com 📌 (posição fixa) ficam no lugar |
| Relógio e clima | Liga ou desliga o relógio e a temperatura na tela |

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

O hardware é da operadora: ela instala cada TV num estabelecimento (host) e
escolhe a playlist.

| Funcionalidade | Detalhe |
|---|---|
| Cadastro | Estabelecimento (host, define o fuso), playlist, nome, local e identificação do aparelho (MAC/série, só informativa) |
| Lista da rede | Todas as TVs, com estabelecimento, playlist e conexão; filtro por estabelecimento e situação; busca por nome, parceiro, local ou aparelho |
| Código de pareamento | 8 caracteres, válido por 24 h, uso único; exibido em destaque com contagem regressiva |
| Conexão | Aguardando pareamento, Online ou Offline (sem sincronizar há 5 min); a lista atualiza a cada minuto |
| Parear outra TV | Gera um código novo; a TV atual continua até a nova parear |
| Desvincular TV | A TV perde o acesso na próxima sincronização e a tela volta a aguardar pareamento |
| Trocar estabelecimento ou playlist | Vale na próxima sincronização; a TV continua pareada |
| Ativar / inativar | Inativa: a TV continua no que já baixou, sem receber grade nova |
| Copiar | "Clonar" cria outra tela no mesmo estabelecimento e local |

## Onde meus anúncios passam (parceiro)

| Funcionalidade | Detalhe |
|---|---|
| Lista | Somente leitura: as TVs ativas cuja playlist tem uma mídia ativa do parceiro, com nome, local, estabelecimento, conexão e última sincronização |

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
| Notícias | Foto em tela cheia, título animado e crédito da fonte; sem foto, fundo da marca. Mais de 48 h, sai da tela mesmo offline |
| Relógio e clima | No canto da tela: hora do estabelecimento e temperatura da cidade dele (Open-Meteo, com crédito). O clima some depois de 6 h sem atualizar |
| Cartões | Previsão do tempo com ícones e cotações com seta de alta ou baixa, em tela cheia, com o crédito da fonte. Funcionam offline enquanto o dado valer (tempo 6 h, cotação 4 dias) |
| QR code | Por cima do anúncio, no canto escolhido, num cartão branco com a legenda; gerado na TV, funciona offline. O médio ocupa 30% da altura da tela |
| Tela de espera | Sem nada para hoje, mostra o nome da tela |
| Informações | Botão OK do controle: conexão, última sincronização, itens de hoje, notícias, clima, servidor e versão |
| Sempre no ar | Tela cheia, sempre acesa, abre sozinho ao ligar a TV |
| Revogação | Desvinculada, continua tocando e pede novo pareamento |

## Segurança e isolamento

- Cada parceiro só vê e altera as próprias mídias. Mídia de outro parceiro,
  ou tela onde ele não passa, responde 404.
- Playlists e mídias da rede só para a operadora (ADR-015).
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
- Contagem de leituras do QR code (o QR é estático e leva direto ao destino).
- Fontes de notícias por playlist (hoje as fontes ativas valem para toda a rede).
- Agendamento por horário do dia (hoje: dia da semana e período).

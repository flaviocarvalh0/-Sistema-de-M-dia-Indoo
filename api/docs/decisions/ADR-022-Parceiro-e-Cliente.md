# ADR-022: Parceiro (Local da TV) e Cliente (Anunciante)

## Status

Aceito. Substitui o uso do termo "parceiro" nos ADRs 011 a 021, que
chamavam de parceiro tanto o local da TV quanto o anunciante.

## Data da decisão

2026-10-18

## Contexto

Até aqui um único cadastro, `Parceiro`, fazia dois papéis:

- **o estabelecimento onde a TV fica** (o "host"): definia o fuso da grade,
  a cidade do clima e aparecia primeiro no diretório da TV;
- **o anunciante**: tinha login no painel, mídias, espaço, plano, perfil do
  diretório e podia ser bloqueado.

No negócio são papéis diferentes. O dono do local cede o espaço e **não usa
o sistema**. Quem anuncia é o cliente da operadora. Misturar os dois obrigava
a criar login e cota para quem só hospeda a TV, e não permitia um local sem
anúncio próprio.

## Decisão

- **Cliente** (`business.cliente`) é o anunciante: o antigo cadastro de
  parceiro, renomeado, com login do responsável (grupo **Cliente**), mídias,
  espaço, plano, perfil do diretório e bloqueio. O painel do anunciante usa
  `IClienteAtual` e as rotas `/api/v1/business/cliente/*`.
- **Parceiro** (`business.parceiro`) é o local da TV: nome, responsável,
  telefone, e-mail de contato, documento, endereço, cidade (clima), fuso
  (grade), observação e ativo. **Não tem acesso.** CRUD só da operadora em
  `/api/v1/business/rede/parceiros` (permissões `Parceiros.*`).
- A tela (`Tela.ParceiroId`) e o clima (`PrevisaoClima.ParceiroId`) apontam
  para o parceiro; a mídia (`Midia.ClienteId`) aponta para o cliente.
- **Parceiro que também anuncia** é cadastrado como cliente e ligado no
  parceiro por `ClienteId` (opcional). O diretório da TV mostra esse cliente
  primeiro (se ele aceitou aparecer). O título da página do diretório é o
  nome do parceiro.
- Parceiro com telas não pode ser excluído (inativar tira da lista de
  escolha; as TVs seguem). Cliente continua sem exclusão: é bloqueado.
- A migration `Business_ClientesEParceiros` **começa limpo**: apaga os
  cadastros antigos (parceiros, telas, mídias, itens de playlist, estados,
  clima) e os logins dos antigos parceiros. Playlists, fontes de notícias e
  versões do app ficam. Os arquivos que estavam no bucket não são apagados
  pela migration.

## Justificativa

- Cada papel com os seus dados: o local não ganha login nem cota que nunca
  vai usar; o anunciante não precisa ser dono de TV.
- A ligação opcional cobre o caso comum do dono que também anuncia sem
  duplicar regras (o perfil do diretório fica num lugar só, o cliente).

Alternativas rejeitadas:

- **Um cadastro com "tipo" (host, anunciante ou ambos):** mantém campos que
  não se aplicam e regras condicionais em todo lugar.
- **Migrar os dados antigos:** eram dados de teste, e cada registro antigo
  teria de virar dois; decidido começar limpo.

## Consequências

### Ficou mais fácil

- Instalar TV num local que não anuncia.
- Um mesmo cliente ter várias unidades (parceiros ligados a ele).

### Ficou mais difícil

- O dono que também anuncia tem dois cadastros (parceiro e cliente) para
  manter ligados.
- Os ADRs anteriores usam "parceiro" no sentido antigo; leia-os com este.

## Referências

- [ADR-014](ADR-014-Posse-das-Telas-pela-Operadora.md) — Posse das telas pela operadora
- [ADR-015](ADR-015-Playlists-da-Operadora-e-Plano-do-Anunciante.md) — Playlists da operadora e plano do anunciante
- [ADR-018](ADR-018-Diretorio-de-Anunciantes-da-TV.md) — Diretório de anunciantes da TV

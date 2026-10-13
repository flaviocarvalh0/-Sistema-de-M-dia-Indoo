# ADR-010: Estratégia de Mensagens e Entrega de E-mails

## Status

Aceito

## Data da decisão

2026-09-02

## Contexto

A Atlas Platform possui atualmente infraestrutura técnica para enfileiramento e envio assíncrono de mensagens de e-mail por SMTP.

O modelo existente utiliza `EmailMessage` como registro da fila técnica e armazena diretamente um único destinatário principal por meio da propriedade `To`.

O envio é processado em segundo plano pelo `EmailProcessingWorker`, que utiliza `IEmailSender` e a implementação `SmtpEmailSender`.

Essa estrutura atende a mensagens simples, porém não suporta de forma adequada necessidades reutilizáveis da plataforma, como:

- múltiplos destinatários;
- destinatários do tipo To, Cc e Bcc;
- anexos;
- recursos inline utilizados por HTML por meio de Content-ID;
- composição técnica de mensagens sem dependência de domínios específicos de clientes;
- separação clara entre o modelo persistido da fila e o contrato utilizado pelo mecanismo de transporte.

A Atlas já possui a infraestrutura transversal `FileContent`, responsável por armazenar conteúdo binário imutável e reutilizável.

Também existe `EntityAttachment`, responsável por associar semanticamente arquivos a registros conhecidos pela plataforma.

É necessário evoluir o mecanismo de e-mail de forma reutilizável, preservando a segregação entre Platform e Business e evitando que aplicações clientes implementem extensões locais da infraestrutura técnica.

## Decisão

A Atlas Platform adotará `EmailMessage` como agregado técnico responsável pela representação persistida de uma mensagem na fila assíncrona de e-mails.

A composição de uma mensagem será formada por:

```text
EmailMessage
├── EmailMessageRecipient
└── EmailMessageAttachment
```

### EmailMessage

`EmailMessage` permanecerá responsável pelas informações e pelo estado técnico do processamento da mensagem.

Deverá conter, entre outras propriedades:

```text
Subject
HtmlBody
Status
ErrorMessage
RetryCount
SentAt
LastAttemptAt
RelatedEntityType
RelatedEntityId
```

A propriedade legada:

```text
To
```

será removida.

Não será mantida compatibilidade estrutural com essa propriedade porque, no momento desta decisão, a plataforma ainda não possui mensagens persistidas que precisem ser migradas ou preservadas.

A propriedade atualmente denominada:

```text
Body
```

passará a representar explicitamente:

```text
HtmlBody
```

### Destinatários

Destinatários serão representados pela entidade:

```text
EmailMessageRecipient
```

Cada destinatário deverá possuir:

```text
EmailMessageId
Name
Email
NormalizedEmail
Type
```

O tipo será representado por enum da própria Platform:

```text
To
Cc
Bcc
```

A Platform não utilizará enums ou conceitos pertencentes aos módulos Business.

Dentro de uma mesma mensagem, o endereço normalizado deverá ser único, independentemente do tipo de destinatário.

Uma mensagem somente poderá ser considerada válida para enfileiramento quando possuir pelo menos um destinatário do tipo `To`.

### Anexos

Anexos e recursos inline serão representados pela entidade:

```text
EmailMessageAttachment
```

Essa entidade não armazenará conteúdo binário próprio.

Cada registro deverá referenciar:

```text
FileContentId
```

reutilizando a infraestrutura de `FileContent` já existente na plataforma.

O vínculo técnico de um arquivo com uma mensagem será independente de `EntityAttachment`.

As responsabilidades serão distintas:

```text
EntityAttachment
→ representa a associação semântica de um arquivo com uma entidade ou registro.

EmailMessageAttachment
→ representa a participação técnica de um arquivo na composição MIME de um e-mail.
```

### Disposição do conteúdo

`EmailMessageAttachment` utilizará um enum técnico da Platform com os valores:

```text
Attachment
Inline
```

Para `Attachment`:

```text
ContentId = null
```

Para `Inline`:

```text
ContentId obrigatório
```

Dentro de uma mesma mensagem, `ContentId` deverá ser único.

### Nome do arquivo

`EmailMessageAttachment` poderá possuir um nome de apresentação opcional.

Quando não informado, será utilizado:

```text
FileContent.FileName
```

Essa propriedade pertence ao vínculo da mensagem e não altera o conteúdo binário persistido.

### Imutabilidade do conteúdo

`FileContent` continuará representando o conteúdo binário imutável.

O mesmo conteúdo poderá ser reutilizado por diferentes mensagens sem duplicação física dos bytes.

As propriedades de composição da mensagem, como:

```text
FileName
Disposition
ContentId
```

permanecerão em `EmailMessageAttachment`.

### Contrato de enfileiramento

Consumidores não deverão criar diretamente entidades `EmailMessage`, `EmailMessageRecipient` ou `EmailMessageAttachment`.

A Platform disponibilizará um contrato público de enfileiramento contendo a descrição da mensagem, seus destinatários e seus anexos.

O serviço:

```text
IEmailMessageService
```

será responsável por:

1. validar a solicitação;
2. construir o agregado;
3. registrar destinatários;
4. registrar anexos;
5. adicionar a mensagem ao `ApplicationDbContext`.

O envio continuará respeitando a unidade de trabalho do consumidor.

### Separação entre persistência e transporte

O contrato utilizado para persistir uma mensagem na fila não será o mesmo contrato utilizado pelo transporte SMTP.

O `EmailProcessingWorker` projetará o estado persistido para um contrato técnico de envio.

Conceitualmente:

```text
EmailMessage
    ↓
EmailSendRequest
    ↓
IEmailSender
    ↓
SmtpEmailSender
```

`IEmailSender` não deverá depender diretamente da entidade EF Core `EmailMessage`.

Essa separação permitirá substituir ou adicionar outros mecanismos de transporte futuramente sem acoplar o modelo persistido à implementação concreta.

### SMTP

`SmtpEmailSender` será responsável por converter o contrato técnico de envio para uma mensagem MIME.

A implementação deverá suportar:

```text
From
To
Cc
Bcc
Subject
HtmlBody
Attachments
Inline Resources
```

Recursos inline utilizarão Content-ID compatível com referências HTML no formato:

```text
cid:<content-id>
```

### Processamento assíncrono

`EmailProcessingWorker` continuará processando mensagens da fila em segundo plano.

O worker deverá carregar os destinatários, anexos e conteúdos necessários antes de chamar `IEmailSender`.

Estados e transições deverão ser controlados por comportamentos do agregado `EmailMessage`, evitando alteração direta indiscriminada de propriedades técnicas pelo worker.

### Retry

O mecanismo continuará registrando:

```text
RetryCount
LastAttemptAt
ErrorMessage
SentAt
Status
```

Não será criada nesta versão uma entidade específica para cada tentativa de envio.

O retry manual será permitido somente para mensagens em estado compatível com reprocessamento.

Mensagens enviadas ou canceladas não deverão ser reabertas pelo mecanismo comum de retry.

### Histórico genérico

`EmailMessage`, `EmailMessageRecipient` e `EmailMessageAttachment` não utilizarão inicialmente `MetadataConfiguration` para habilitar histórico genérico.

O mecanismo existente de histórico por snapshots não será utilizado para registrar tentativas de SMTP ou versões do corpo de e-mail.

Caso seja necessária telemetria detalhada por tentativa, uma modelagem específica deverá ser avaliada futuramente.

### Observabilidade

Conteúdos e dados sensíveis deverão ser protegidos pelo mecanismo de sanitização da Platform.

No mínimo:

```text
EmailMessage.HtmlBody
→ omitido dos logs estruturados.

EmailMessageRecipient.Email
EmailMessageRecipient.NormalizedEmail
EmailMessageRecipient.Name
→ tratados como dados sensíveis.

FileContent.Content
→ omitido dos logs estruturados.

SmtpConfiguration.Password
SmtpConfiguration.UserName
SmtpConfiguration.SenderEmail
→ tratados como dados sensíveis.
```

O mecanismo de logs não deverá serializar conteúdos binários ou HTMLs completos.

### Independência de Business

Nenhuma entidade, enum, serviço ou contrato desta funcionalidade poderá depender de conceitos pertencentes a aplicações clientes.

A direção permitida continuará sendo:

```text
Business → contratos públicos de Platform

Platform -X→ Business
```

Conceitos como distribuição de comunicação, cliente monitorado, relatório, contato ou produto não farão parte deste mecanismo.

### Persistência

As novas tabelas utilizarão o schema:

```text
platform
```

com nomenclatura em `snake_case`.

Serão adicionadas, no mínimo:

```text
platform.email_message_recipient
platform.email_message_attachment
```

A migration deverá utilizar prefixo arquitetural `Platform_`.

### Controle de concorrência

As novas entidades derivadas de `BaseEntity` utilizarão a convenção global de concorrência otimista baseada em `xmin`.

As configurações individuais não deverão repetir o mapeamento de `Version`.

## Justificativa

### Alternativa: manter um campo To e adicionar Cc/Bcc como strings

Foi rejeitada porque produziria campos especiais por tipo de destinatário, dificultaria validação, pesquisa, normalização e múltiplos endereços.

Também impediria modelar adequadamente atributos próprios de cada destinatário.

### Alternativa: armazenar listas de e-mail separadas por delimitador

Foi rejeitada porque eliminaria integridade relacional, dificultaria filtros e não permitiria índices ou unicidade por endereço.

### Alternativa: armazenar bytes diretamente em EmailMessageAttachment

Foi rejeitada porque duplicaria a infraestrutura já fornecida por `FileContent` e aumentaria desnecessariamente o armazenamento.

### Alternativa: utilizar EntityAttachment diretamente como anexo técnico

Foi rejeitada porque `EntityAttachment` representa vínculo semântico entre arquivos e registros, enquanto um attachment MIME representa a composição técnica de uma mensagem.

Os conceitos podem apontar para o mesmo `FileContent`, mas possuem ciclos de vida e responsabilidades diferentes.

### Alternativa: fazer IEmailSender receber EmailMessage

Foi rejeitada porque acoplaria o mecanismo de transporte ao modelo persistido e ao Entity Framework Core.

A adoção de um contrato técnico intermediário preserva a possibilidade de outros mecanismos de transporte.

### Alternativa: implementar a evolução diretamente em uma aplicação cliente

Foi rejeitada conforme o ADR-007.

Capacidades reutilizáveis da Platform devem ser implementadas e versionadas primeiro no repositório oficial da Atlas Platform e incorporadas posteriormente pelos produtos consumidores.

### Alternativa: utilizar o histórico genérico para registrar tentativas de SMTP

Foi rejeitada porque o histórico por snapshots foi projetado para representar versões completas de registros.

Utilizá-lo para retries causaria cópias repetidas do corpo da mensagem e não representaria adequadamente telemetria técnica de tentativa.

## Consequências

### Ficou mais fácil

- Enviar mensagens para múltiplos destinatários;
- Suportar To, Cc e Bcc de maneira relacional;
- Pesquisar mensagens por destinatário;
- Reutilizar conteúdos binários já persistidos;
- Enviar múltiplos anexos;
- Utilizar imagens e outros recursos inline por CID;
- Manter Business independente da implementação SMTP;
- Substituir ou adicionar transportes futuros;
- Validar invariantes no agregado;
- Reduzir risco de exposição de conteúdos e dados pessoais nos logs;
- Consumir a infraestrutura de e-mail em diferentes produtos Atlas.

### Ficou mais difícil

- O agregado `EmailMessage` passa a possuir entidades filhas;
- O worker precisará carregar e projetar mais dados;
- O envio SMTP precisará montar uma árvore MIME mais complexa;
- Consultas administrativas precisarão considerar destinatários relacionais;
- Novas constraints e índices precisarão ser mantidos;
- O contrato público de enfileiramento exigirá versionamento cuidadoso.

## Observações de implementação

- `EmailMessageRecipient` e `EmailMessageAttachment` pertencem ao namespace da Platform;
- Todos os tipos técnicos da Platform serão nomeados em inglês;
- O schema PostgreSQL será `platform`;
- As tabelas e colunas utilizarão `snake_case`;
- `FileContent.Content` deverá ser protegido contra serialização acidental em logs;
- A rota de retry de `EmailMessage` deverá utilizar constraint compatível com IDs do tipo `long`;
- O endpoint SMTP deverá permanecer na fronteira de endpoints da Platform;
- A geração da migration deverá seguir o processo documentado de migrations da Atlas;
- A implementação deverá ser validada sem dependência de nenhuma aplicação Business;
- A versão planejada para disponibilização desta funcionalidade é `v0.4.0-alpha.1`.

## Referências

- ADR-003: Segregação Arquitetural entre Plataforma e Negócio;
- ADR-004: Convenções de Idioma e Nomenclatura;
- ADR-005: Estratégia de Schemas no PostgreSQL;
- ADR-006: Contexto Único e Migrations Unificadas;
- ADR-007: Plataforma-base e Evolução dos Produtos de Cliente;
- ADR-008: Gestão de Configurações e Segredos;
- ADR-009: Estratégia de Versionamento Histórico de Entidades.

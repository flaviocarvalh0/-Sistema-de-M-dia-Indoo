# ADR-008: Gestão de Configurações e Segredos

## Status

Aceito

## Data da decisão

2026-07-18

## Contexto

As aplicações da plataforma Atlas utilizam configurações que variam entre os ambientes de desenvolvimento, homologação e produção.

Até o momento, a equipe não possuía uma convenção formal para separar configurações comuns, configurações específicas de ambiente e valores sensíveis. Isso permitia que connection strings, chaves JWT, tokens ou outras credenciais fossem inseridos diretamente em arquivos `appsettings`, com risco de exposição no repositório Git, no histórico de commits e nos artefatos de publicação.

Também era necessário definir:

- Qual responsabilidade pertence ao `appsettings.json`;
- Quando utilizar `appsettings.{Environment}.json`;
- Como cada desenvolvedor deve configurar segredos locais;
- Como homologação e produção devem receber seus valores;
- Como novas configurações devem ser documentadas e validadas;
- Como evitar que a aplicação inicie com configurações obrigatórias ausentes.

A solução deve permanecer simples para o desenvolvimento local, segura para publicação e compatível com diferentes mecanismos de implantação.

## Decisão

A Atlas adotará uma separação explícita entre configurações não sensíveis e segredos.

### Configurações comuns

O arquivo `appsettings.json` conterá somente configurações comuns e não sensíveis, utilizadas como base por todos os ambientes.

Exemplos:

- Nome da aplicação;
- Issuer e Audience do JWT;
- Duração de tokens;
- Configurações funcionais;
- Níveis padrão de log;
- Fuso horário;
- Limites, parâmetros e comportamentos padrão.

### Configurações específicas de ambiente

Arquivos no formato:

```text
appsettings.{Environment}.json
```

conterão somente diferenças não sensíveis do respectivo ambiente.

Exemplos:

```text
appsettings.Development.json
appsettings.Staging.json
appsettings.Production.json
```

Esses arquivos poderão ser versionados, desde que não contenham credenciais ou outros segredos.

A existência de um `appsettings.Production.json` será opcional. Quando não houver diferenças públicas específicas de produção, o ambiente utilizará o `appsettings.json` como base.

### Segredos de desenvolvimento

Segredos utilizados durante o desenvolvimento local serão armazenados com o Secret Manager do .NET.

Exemplos:

```text
ConnectionStrings:AtlasDb
Jwt:Key
Smtp:Password
ExternalServices:ApiKey
```

Os valores permanecerão fora da pasta do repositório. Apenas o `UserSecretsId` do projeto poderá ser versionado.

### Segredos de homologação e produção

Segredos de homologação e produção serão fornecidos pela infraestrutura de implantação por meio de:

- Variáveis de ambiente;
- Cofres de segredos;
- Secrets da plataforma de hospedagem;
- Docker Secrets;
- Kubernetes Secrets;
- Mecanismos equivalentes aprovados pela equipe.

Segredos reais não serão armazenados em arquivos versionados nem incorporados ao artefato publicado.

### Precedência de configuração

Será respeitada a precedência padrão da configuração do ASP.NET Core.

De forma simplificada:

```text
appsettings.json
appsettings.{Environment}.json
User Secrets em Development
Variáveis de ambiente
Argumentos de linha de comando
```

Fontes carregadas posteriormente poderão sobrescrever valores anteriores.

### Convenção para variáveis de ambiente

A separação hierárquica das configurações será representada por dois caracteres de sublinhado.

Exemplos:

```text
ConnectionStrings__AtlasDb
Jwt__Key
Smtp__Password
```

### Validação

Configurações obrigatórias deverão ser validadas durante a inicialização da aplicação.

A aplicação não deverá iniciar com valores obrigatórios ausentes, vazios, inválidos ou substituídos por placeholders de exemplo.

A estratégia técnica de validação será definida e implementada separadamente, preservando esta decisão como regra arquitetural.

### Documentação

A política completa de configuração e segredos será mantida em:

```text
docs/configuracao/Configuracao-de-Ambientes-e-Segredos.md
```

As instruções para execução local serão mantidas em:

```text
docs/desenvolvimento/Configuracao-Local.md
```

O `README.md` da raiz deverá referenciar esses documentos.

### Exposição de segredos

Qualquer segredo enviado ao Git será considerado comprometido.

Nessa situação, a equipe deverá:

1. Remover o valor dos arquivos atuais;
2. Revogar ou substituir a credencial;
3. Avaliar a limpeza do histórico;
4. Verificar possíveis usos indevidos;
5. Registrar e comunicar o incidente aos responsáveis.

A remoção em um commit posterior não será considerada suficiente para tornar o segredo seguro novamente.

## Justificativa

### Alternativa: armazenar todos os valores no `appsettings.json`

Foi rejeitada porque mistura configurações públicas com segredos, aumenta o risco de exposição e acopla o artefato publicado a um ambiente específico.

### Alternativa: mover os segredos para `appsettings.Development.json`

Foi rejeitada porque esse arquivo também pode ser versionado. A mudança de arquivo não elimina o risco de exposição no Git.

### Alternativa: criar arquivos de produção com credenciais reais

Foi rejeitada porque senhas, tokens e chaves passariam a fazer parte do código-fonte, do histórico, dos backups e potencialmente do pacote de publicação.

### Alternativa: utilizar somente variáveis de ambiente em todos os ambientes

Foi rejeitada como convenção única porque aumentaria desnecessariamente o custo de configuração local e reduziria a legibilidade das configurações públicas.

A combinação entre arquivos versionados para valores não sensíveis, User Secrets no desenvolvimento e mecanismos protegidos na implantação oferece separação clara sem introduzir infraestrutura excessiva.

### Alternativa: manter o processo informal

Foi rejeitada porque depende de conhecimento individual, dificulta a entrada de novos colaboradores e não oferece critérios verificáveis durante revisão e implantação.

## Consequências

### Ficou mais fácil

- Publicar o código sem expor credenciais;
- Identificar a responsabilidade de cada arquivo de configuração;
- Preparar ambientes independentes;
- Alterar segredos sem recompilar a aplicação;
- Orientar novos desenvolvedores;
- Revisar Pull Requests;
- Automatizar implantações;
- Evoluir futuramente para cofres de segredos;
- Validar configurações obrigatórias de forma consistente.

### Ficou mais difícil

- Cada desenvolvedor precisará configurar seus próprios User Secrets;
- A infraestrutura de implantação deverá fornecer valores obrigatórios;
- O projeto não funcionará imediatamente após o clone sem configuração local;
- A equipe precisará manter a documentação atualizada;
- Segredos expostos exigirão rotação, mesmo após remoção do arquivo;
- Novas configurações precisarão ser classificadas antes de serem adicionadas.

## Observações de implementação

### Exemplo de `appsettings.json`

```json
{
  "Jwt": {
    "Issuer": "Atlas",
    "Audience": "Atlas.Clients",
    "AccessTokenMinutes": 30
  },
  "PlatformSettings": {
    "ForceLocalTimeZone": true,
    "DefaultTimeZoneId": "E. South America Standard Time"
  },
  "AllowedHosts": "*"
}
```

### Exemplo de User Secrets

```bash
dotnet user-secrets set "ConnectionStrings:AtlasDb" "CONNECTION_STRING_LOCAL"
dotnet user-secrets set "Jwt:Key" "CHAVE_JWT_LOCAL"
```

### Exemplo de produção

```text
ASPNETCORE_ENVIRONMENT=Production
ConnectionStrings__AtlasDb=<CONNECTION_STRING_DE_PRODUCAO>
Jwt__Key=<CHAVE_JWT_DE_PRODUCAO>
```

### Arquivos que não devem ser versionados

O `.gitignore` deverá excluir, no mínimo:

```gitignore
**/bin/
**/obj/
**/logs/
*.csproj.user
.env
.env.*
!.env.example
```

## Referências

- `docs/deployment/Configuracao-de-Ambientes-e-Segredos.md`
- `docs/development/Configuracao-Local.md`
- Documentação do ASP.NET Core sobre configuração;
- Documentação do ASP.NET Core sobre ambientes;
- Documentação do .NET Secret Manager.

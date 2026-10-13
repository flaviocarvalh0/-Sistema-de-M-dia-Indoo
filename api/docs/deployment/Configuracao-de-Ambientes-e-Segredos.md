# Configuração de Ambientes e Gestão de Segredos

## Objetivo

Este documento define como as aplicações da plataforma Atlas devem organizar configurações, valores específicos de ambiente e segredos.

O procedimento busca:

- Evitar o versionamento de credenciais;
- Separar configurações comuns de diferenças de ambiente;
- Padronizar o desenvolvimento local;
- Preparar a aplicação para homologação e produção;
- Reduzir erros de configuração;
- Facilitar revisão, implantação e entrada de novos colaboradores.

## Escopo

Este procedimento aplica-se a projetos executáveis da solução, incluindo:

- APIs;
- Aplicações ASP.NET Core;
- Workers;
- Serviços em segundo plano;
- Aplicações que utilizem banco de dados;
- Aplicações que emitam ou validem JWT;
- Aplicações que consumam serviços externos.

Bibliotecas de domínio, aplicação ou infraestrutura que não inicializem um host não devem possuir arquivos `appsettings` próprios sem necessidade arquitetural explícita.

## Princípios

### Configurações não sensíveis

Configurações não sensíveis são valores que podem ser conhecidos por colaboradores autorizados sem conceder acesso indevido a sistemas ou dados.

Exemplos:

- Issuer e Audience;
- Duração de tokens;
- Fuso horário;
- Nível padrão de log;
- Tamanho padrão de página;
- Parâmetros funcionais;
- URLs públicas;
- Nomes de recursos.

Esses valores podem ser versionados em arquivos `appsettings`.

### Segredos

Segredos são valores que permitem autenticação, acesso ou operação privilegiada.

Exemplos:

- Senhas de banco de dados e de usuários iniciais (ex: Admin);
- Connection strings com credenciais;
- Chaves JWT;
- Tokens;
- Chaves de API;
- Credenciais SMTP;
- Chaves privadas;
- Senhas de certificados.

Segredos não devem ser armazenados em arquivos versionados.

## Responsabilidade dos arquivos

### `appsettings.json`

Contém a configuração base, comum e não sensível.

Exemplo:

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

Não deve conter:

- Senhas (nem mesmo a `InitialAdminPassword`);
- Chaves JWT reais;
- Tokens;
- Connection strings com credenciais;
- Credenciais de serviços externos.

### `appsettings.Development.json`

Contém diferenças não sensíveis do ambiente de desenvolvimento.

Exemplos:

- Log mais detalhado;
- Escrita de log em arquivo local;
- URLs locais ou de sandbox;
- Recursos simulados;
- Configurações auxiliares de depuração.

Esse arquivo pode ser versionado e, por isso, não deve conter segredos.

### `appsettings.Staging.json`

É opcional e pode conter diferenças não sensíveis do ambiente de homologação.

Segredos de homologação devem ser fornecidos pela infraestrutura.

### `appsettings.Production.json`

É opcional e pode conter diferenças não sensíveis de produção.

Exemplos:

```json
{
  "PlatformSettings": {
    "ForceLocalTimeZone": false,
    "DefaultTimeZoneId": "UTC"
  },
  "Serilog": {
    "MinimumLevel": {
      "Default": "Information"
    }
  }
}
```

Esse arquivo não deve conter credenciais reais.

## Ordem de carregamento

A aplicação combina configurações de diferentes fontes.

De forma simplificada:

```text
appsettings.json
appsettings.{Environment}.json
User Secrets em Development
Variáveis de ambiente
Argumentos de linha de comando
```

Fontes posteriores sobrescrevem valores anteriores.

Não é necessário repetir a configuração inteira em cada arquivo. Cada arquivo de ambiente deve conter apenas as diferenças necessárias.

## Desenvolvimento local

### Inicializar o Secret Manager

Na pasta do projeto executável:

```bash
dotnet user-secrets init
```

O comando adiciona um `UserSecretsId` ao arquivo `.csproj`.

Esse identificador pode ser versionado. Os valores ficam fora do repositório.

### Configurar a conexão local

```bash
dotnet user-secrets set "ConnectionStrings:AtlasDb" "CONNECTION_STRING_LOCAL"
```

### Configurar a chave JWT

```bash
dotnet user-secrets set "Jwt:Key" "CHAVE_JWT_LOCAL"
```

### Configurar a senha inicial do Administrador

```bash
dotnet user-secrets set "PlatformSettings:InitialAdminPassword" "SENHA_ADMIN_LOCAL"
```

### Listar os segredos

```bash
dotnet user-secrets list
```

### Remover um segredo

```bash
dotnet user-secrets remove "Jwt:Key"
```

### Remover todos os segredos

```bash
dotnet user-secrets clear
```

## Homologação e produção

O Secret Manager não deve ser utilizado fora do desenvolvimento local.

Segredos de homologação e produção devem ser fornecidos por mecanismos protegidos, como:

- Variáveis de ambiente;
- Cofre de segredos;
- Configuração protegida da hospedagem;
- Docker Secrets;
- Kubernetes Secrets;
- GitHub Actions Secrets;
- Serviço equivalente aprovado pela equipe.

### Convenção de nomes

Nas variáveis de ambiente, a hierarquia da configuração é representada por dois caracteres de sublinhado.

```text
ConnectionStrings__AtlasDb
Jwt__Key
Smtp__Password
PlatformSettings__InitialAdminPassword
```

Equivalência:

```text
ConnectionStrings__AtlasDb
```

```json
{
  "ConnectionStrings": {
    "AtlasDb": "valor"
  }
}
```

### Valores mínimos de produção

Exemplo:

```text
ASPNETCORE_ENVIRONMENT=Production
ConnectionStrings__AtlasDb=<CONNECTION_STRING_DE_PRODUCAO>
Jwt__Key=<CHAVE_JWT_DE_PRODUCAO>
PlatformSettings__InitialAdminPassword=<SENHA_ADMIN_PRODUCAO>
```

As variáveis adicionais dependerão dos módulos e integrações habilitados.

## Classificação de novas configurações

Ao adicionar uma nova configuração, a equipe deverá responder às seguintes perguntas.

### O valor é sensível?

Quando for sensível:

```text
Desenvolvimento → User Secrets
Homologação e produção → mecanismo protegido da infraestrutura
```

Quando não for sensível, continuar a classificação.

### O valor é comum a todos os ambientes?

Quando for comum:

```text
appsettings.json
```

Quando for específico:

```text
appsettings.{Environment}.json
```

### O valor é obrigatório?

Quando for obrigatório, deverá existir validação durante a inicialização.

A aplicação não deverá iniciar com valor ausente, vazio, inválido ou de exemplo.

## Regras de segurança

### Proibido versionar

- Senhas;
- Connection strings com senha;
- Chaves JWT reais;
- Tokens;
- Chaves privadas;
- Certificados contendo chave privada;
- Arquivos `.env` com valores reais;
- Credenciais em código, testes ou scripts.

### Valores por ambiente

Desenvolvimento, homologação e produção devem utilizar credenciais diferentes.

A chave JWT de desenvolvimento não deve ser reutilizada em produção, assim como a senha do administrador local não deve ser a mesma de produção.

### Segredo exposto

Um segredo enviado ao Git deve ser considerado comprometido.

Procedimento:

1. Remover o valor dos arquivos atuais;
2. Revogar ou substituir a credencial;
3. Gerar novo valor;
4. Avaliar a limpeza do histórico;
5. Comunicar os responsáveis;
6. Verificar possível uso indevido.

Remover o valor em um commit posterior não elimina a exposição anterior.

## Revisão antes do Pull Request

Antes de abrir um Pull Request:

- Revisar `git diff`;
- Revisar `git status`;
- Confirmar que não existem segredos nos `appsettings`;
- Confirmar que não existem credenciais em testes ou scripts;
- Confirmar que arquivos `.env` reais não foram incluídos;
- Confirmar que logs não foram incluídos;
- Documentar novas configurações;
- Garantir que configurações obrigatórias sejam validadas.

## `.gitignore`

O repositório deve excluir, no mínimo:

```gitignore
# Build
**/bin/
**/obj/

# Logs
**/logs/
*.log

# Configurações locais
.env
.env.*
!.env.example

# Visual Studio
.vs/
*.user
*.suo
*.csproj.user

# Rider
.idea/

# Sistema operacional
.DS_Store
Thumbs.db
```

## Responsabilidades

### Desenvolvedores

- Não versionar segredos;
- Utilizar User Secrets localmente;
- Documentar novas configurações;
- Revisar alterações antes do commit;
- Comunicar exposições imediatamente.

### Responsáveis pela implantação

- Configurar valores do ambiente;
- Proteger credenciais;
- Limitar acesso aos segredos;
- Rotacionar credenciais quando necessário;
- Garantir o ambiente correto.

### Revisores

- Verificar presença de segredos;
- Conferir a localização das configurações;
- Impedir aprovação de credenciais versionadas;
- Verificar documentação e validação.

## Resumo

```text
appsettings.json
Configurações comuns e não sensíveis.

appsettings.Development.json
Diferenças não sensíveis do desenvolvimento.

appsettings.Staging.json
Diferenças não sensíveis da homologação, quando necessário.

appsettings.Production.json
Diferenças não sensíveis da produção, quando necessário.

User Secrets
Segredos do desenvolvimento local.

Variáveis de ambiente ou cofre de segredos
Segredos de homologação e produção.
```

A regra principal é:

> Nenhum segredo real deve fazer parte do código-fonte, do histórico do Git ou do artefato publicado.
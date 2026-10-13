using Atlas.Web.Business;
using Atlas.Infrastructure.Data;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

builder.AddApplicationServices();
builder.AddInfrastructureServices();
builder.AddIndoor();
builder.AddWebServices();

var app = builder.Build();

// Interceptador global de exceções (Sempre ativo para centralizar o tratamento no ProblemDetailsExceptionHandler)
app.UseExceptionHandler();

// =========================================================================
// Inicialização do Banco de Dados e Governança de Infraestrutura
// =========================================================================

// 1. Executa em QUALQUER ambiente (Dev, Staging, Prod):
// Aplica Migrations, sincroniza o catálogo em platform.entity_metadata e provisiona/atualiza as triggers do histórico.
await app.InitialiseDatabaseAsync();

// 2. Executa APENAS em ambiente de Desenvolvimento:
// Semeia dados iniciais para testes (usuário admin padrão, ícones, etc.)
if (app.Environment.IsDevelopment())
{
    await app.SeedDatabaseAsync();
}
else
{
    app.UseHsts();
}

app.UseHttpsRedirection();

// Configuração de CORS (obrigatório vir antes dos mapeamentos de rota e autenticação)
app.UseCors(policy =>
    policy
        .WithOrigins(
            "https://localhost:7116",
            "http://localhost:5266",
            "http://localhost:4200")
        .AllowAnyMethod()
        .AllowAnyHeader()
        .AllowCredentials()); // Permite o tráfego seguro de Cookies HTTPOnly entre domínios

// =========================================================================
// Pipeline de Segurança e Governança de Identidade (A ordem aqui é crítica)
// =========================================================================

// 1. Processa e descriptografa os mecanismos de autenticação (Cookies/JWT) para remontar o ClaimsPrincipal
app.UseAuthentication();

// 2. Interceptador soberano que barra acessos à API se o usuário possuir pendência de alteração de senha
app.UseMiddleware<PasswordChangeMiddleware>();

// 3. Avalia as políticas de RBAC e permissões baseadas em claims para os usuários liberados pelo fluxo acima
app.UseAuthorization();

// Mídia Indoor: middlewares do produto (ver Atlas.Web.Business.IndoorComposicao)
app.UseIndoor();

// =========================================================================

// Servidor de arquivos estáticos (se aplicável para SPAs ou uploads locais)
app.UseFileServer();

// Geração e interface da documentação OpenAPI/Scalar
app.MapOpenApi();
app.MapScalarApiReference();

// Mapeamento automático dos REQS/Endpoints da aplicação (Minimal APIs / Carter / Custom)
app.MapEndpoints(typeof(Program).Assembly);

app.Run();

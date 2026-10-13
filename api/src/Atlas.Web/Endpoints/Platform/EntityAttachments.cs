using System.Globalization;
using Atlas.Application.Common.Exceptions;
using Atlas.Application.Platform.EntityAttachments.Commands.CreateEntityAttachment;
using Atlas.Application.Platform.EntityAttachments.Commands.DeleteEntityAttachment;
using Atlas.Application.Platform.EntityAttachments.Commands.UpdateEntityAttachment;
using Atlas.Application.Platform.EntityAttachments.Common;
using Atlas.Application.Platform.EntityAttachments.Queries.GetEntityAttachmentContent;
using Atlas.Application.Platform.EntityAttachments.Queries.GetEntityAttachments;
using FluentValidation.Results;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Atlas.Web.Endpoints.Platform;

public class EntityAttachments : IEndpointGroup
{
    public static void Map(RouteGroupBuilder groupBuilder)
    {
        var attachmentGroup = groupBuilder
            .WithTags("Anexos de Entidades");

        attachmentGroup
            .MapGet(
                "{routeKey}/{recordId:long}",
                GetEntityAttachments)
            .RequireAuthorization();

        attachmentGroup
            .MapPost(
                "{routeKey}/{recordId:long}",
                CreateEntityAttachment)
            .Accepts<CreateEntityAttachmentRequest>(
                "multipart/form-data")
            .RequireAuthorization();

        attachmentGroup
            .MapPut(
                "{id:long}",
                UpdateEntityAttachment)
            .RequireAuthorization();

        attachmentGroup
            .MapDelete(
                "{id:long}",
                DeleteEntityAttachment)
            .RequireAuthorization();

        attachmentGroup
            .MapGet(
                "{id:long}/content",
                GetEntityAttachmentContent)
            .RequireAuthorization();
    }

    [EndpointSummary("Consultar anexos da entidade")]
    [EndpointDescription(
        "Obtém os arquivos anexados a um determinado registro, " +
        "identificado pela chave pública da entidade e pelo identificador do registro.")]
    public static async Task<Ok<IReadOnlyCollection<EntityAttachmentDto>>>
        GetEntityAttachments(
            string routeKey,
            long recordId,
            ISender sender,
            CancellationToken cancellationToken)
    {
        var query = new GetEntityAttachmentsQuery
        {
            RouteKey = routeKey,
            RecordId = recordId
        };

        var result = await sender.Send(
            query,
            cancellationToken);

        return TypedResults.Ok(result);
    }

    [EndpointSummary("Adicionar anexo à entidade")]
    [EndpointDescription(
        "Adiciona um arquivo a um registro existente. " +
        "A requisição utiliza multipart/form-data. " +
        "Título, descrição e data de referência são opcionais.")]
    public static async Task<Created<EntityAttachmentDto>>
        CreateEntityAttachment(
            string routeKey,
            long recordId,
            HttpRequest request,
            ISender sender,
            CancellationToken cancellationToken)
    {
        if (!request.HasFormContentType)
        {
            ThrowValidation(
                "File",
                "A requisição deve utilizar o formato multipart/form-data.");
        }

        var form =
            await request.ReadFormAsync(
                cancellationToken);

        if (form.Files.Count == 0)
        {
            ThrowValidation(
                "File",
                "O arquivo é obrigatório.");
        }

        if (form.Files.Count > 1)
        {
            ThrowValidation(
                "File",
                "Apenas um arquivo pode ser enviado por requisição.");
        }

        var file =
            form.Files[0];

        if (file.Length == 0)
        {
            ThrowValidation(
                "File",
                "O conteúdo do arquivo não pode estar vazio.");
        }

        await using var memoryStream =
            new MemoryStream();

        await file.CopyToAsync(
            memoryStream,
            cancellationToken);

        var referenceDate =
            ParseReferenceDate(
                form["referenceDate"].FirstOrDefault());

        var command =
            new CreateEntityAttachmentCommand
            {
                RouteKey =
                    routeKey,

                RecordId =
                    recordId,

                FileName =
                    file.FileName,

                ContentType =
                    string.IsNullOrWhiteSpace(
                        file.ContentType)
                        ? "application/octet-stream"
                        : file.ContentType,

                Content =
                    memoryStream.ToArray(),

                Title =
                    NormalizeOptional(
                        form["title"].FirstOrDefault()),

                Description =
                    NormalizeOptional(
                        form["description"].FirstOrDefault()),

                ReferenceDate =
                    referenceDate
            };

        var result =
            await sender.Send(
                command,
                cancellationToken);

        return TypedResults.Created(
            $"/entity-attachments/{result.Id}",
            result);
    }

    [EndpointSummary("Alterar detalhes do anexo")]
    [EndpointDescription(
        "Altera os metadados de um anexo existente. " +
        "O conteúdo físico do arquivo não pode ser substituído. " +
        "A versão atual deve ser informada para controle de concorrência otimista.")]
    public static async Task<Ok<EntityAttachmentDto>>
        UpdateEntityAttachment(
            long id,
            UpdateEntityAttachmentRequest request,
            ISender sender,
            CancellationToken cancellationToken)
    {
        var command =
            new UpdateEntityAttachmentCommand
            {
                Id = id,
                Version = request.Version,
                Title = request.Title,
                Description = request.Description,
                ReferenceDate = request.ReferenceDate
            };

        var result = await sender.Send(
            command,
            cancellationToken);

        /*
         * Após o UPDATE, o PostgreSQL atribui um novo xmin.
         * O EntityAttachmentDto devolve essa nova Version ao
         * frontend para a próxima alteração/exclusão.
         */
        return TypedResults.Ok(result);
    }

    [EndpointSummary("Excluir anexo")]
    [EndpointDescription(
        "Exclui um anexo e, quando não houver outras referências, " +
        "remove também seu conteúdo físico. " +
        "A versão deve ser informada para controle de concorrência otimista.")]
    public static async Task<Ok<DeleteEntityAttachmentResponse>>
        DeleteEntityAttachment(
            long id,
            uint version,
            ISender sender,
            CancellationToken cancellationToken)
    {
        var command =
            new DeleteEntityAttachmentCommand(
                id,
                version);

        var result = await sender.Send(
            command,
            cancellationToken);

        return TypedResults.Ok(result);
    }

    [EndpointSummary("Baixar conteúdo do anexo")]
    [EndpointDescription(
        "Obtém o conteúdo binário de um anexo utilizando o tipo de conteúdo " +
        "e o nome original armazenados pela plataforma.")]
    public static async Task<FileContentHttpResult>
        GetEntityAttachmentContent(
            long id,
            ISender sender,
            CancellationToken cancellationToken)
    {
        var query =
            new GetEntityAttachmentContentQuery(id);

        var result = await sender.Send(
            query,
            cancellationToken);

        return TypedResults.File(
            result.Content,
            result.ContentType,
            result.FileName);
    }

    private static DateTimeOffset? ParseReferenceDate(
        string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        if (DateTimeOffset.TryParse(
            value,
            CultureInfo.InvariantCulture,
            DateTimeStyles.RoundtripKind,
            out var referenceDate))
        {
            return referenceDate;
        }

        ThrowValidation(
            "ReferenceDate",
            "A data de referência informada é inválida.");

        return null;
    }

    private static string? NormalizeOptional(
        string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? null
            : value.Trim();
    }

    private static void ThrowValidation(
        string propertyName,
        string message)
    {
        throw new ValidationException(
            [
                new ValidationFailure(
                    propertyName,
                    message)
            ]);
    }

    public sealed class CreateEntityAttachmentRequest
    {
        public required IFormFile File { get; init; }

        public string? Title { get; init; }

        public string? Description { get; init; }

        public DateTimeOffset? ReferenceDate { get; init; }
    }

    public sealed record UpdateEntityAttachmentRequest
    {
        public required uint Version { get; init; }
        public string? Title { get; init; }
        public string? Description { get; init; }
        public DateTimeOffset? ReferenceDate { get; init; }
    }
}

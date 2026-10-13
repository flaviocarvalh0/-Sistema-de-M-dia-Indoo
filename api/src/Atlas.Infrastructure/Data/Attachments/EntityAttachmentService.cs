using System.Security.Cryptography;
using Atlas.Application.Common.Interfaces;
using Atlas.Application.Platform.EntityAttachments.Common;
using Atlas.Domain.Platform.Entities;

namespace Atlas.Infrastructure.Data.Attachments;

internal sealed class EntityAttachmentService
    : IEntityAttachmentService
{
    private readonly ApplicationDbContext _context;

    public EntityAttachmentService(
        ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<EntityAttachmentDto> CreateAsync(
        CreateEntityAttachmentRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (string.IsNullOrWhiteSpace(request.RouteKey))
        {
            throw new ArgumentException(
                "A chave de rota da entidade é obrigatória.",
                nameof(request));
        }

        if (request.RecordId <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(request.RecordId),
                "O identificador do registro deve ser maior que zero.");
        }

        if (string.IsNullOrWhiteSpace(request.FileName))
        {
            throw new ArgumentException(
                "O nome do arquivo é obrigatório.",
                nameof(request));
        }

        if (string.IsNullOrWhiteSpace(request.ContentType))
        {
            throw new ArgumentException(
                "O tipo de conteúdo do arquivo é obrigatório.",
                nameof(request));
        }

        ArgumentNullException.ThrowIfNull(request.Content);

        if (request.Content.Length == 0)
        {
            throw new ArgumentException(
                "O conteúdo do arquivo não pode estar vazio.",
                nameof(request));
        }

        var routeKey =
            request.RouteKey.Trim();

        var metadata =
            await _context.EntityMetadata
                .AsNoTracking()
                .SingleOrDefaultAsync(
                    metadata =>
                        metadata.RouteKey == routeKey,
                    cancellationToken)
            ?? throw new KeyNotFoundException(
                $"Nenhum metadado de entidade foi encontrado para a rota '{routeKey}'.");

        var fileName =
            request.FileName.Trim();

        var extension =
            ResolveExtension(fileName);

        var hash =
            ComputeHash(request.Content);

        var fileContent =
            new FileContent(
                fileName,
                request.ContentType,
                extension,
                hash,
                request.Content);

        var attachment =
            new EntityAttachment(
                metadata.Id,
                request.RecordId,
                fileContent,
                request.Title,
                request.Description,
                request.ReferenceDate);

        /*
         * O FileContent novo faz parte do graph rastreado pelo
         * EntityAttachment. O EF Core insere primeiro file_content
         * e depois entity_attachment no mesmo SaveChanges.
         */
        _context.EntityAttachments.Add(
            attachment);

        await _context.SaveChangesAsync(
            cancellationToken);

        /*
         * A consulta posterior é proposital:
         * além dos dados de auditoria preenchidos pelo interceptor,
         * precisamos devolver também o xmin/Version efetivamente
         * atribuído pelo PostgreSQL e os nomes dos usuários.
         */
        return await GetAttachmentDtoAsync(
            attachment.Id,
            cancellationToken);
    }

    public async Task<IReadOnlyCollection<EntityAttachmentDto>>
        GetAsync(
            string routeKey,
            long recordId,
            CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(
            routeKey);

        if (recordId <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(recordId),
                "O identificador do registro deve ser maior que zero.");
        }

        var normalizedRouteKey =
            routeKey.Trim();

        var metadataId =
            await _context.EntityMetadata
                .AsNoTracking()
                .Where(metadata =>
                    metadata.RouteKey == normalizedRouteKey)
                .Select(metadata =>
                    (long?)metadata.Id)
                .SingleOrDefaultAsync(
                    cancellationToken)
            ?? throw new KeyNotFoundException(
                $"Nenhum metadado de entidade foi encontrado para a rota '{normalizedRouteKey}'.");

        /*
         * Os LEFT JOINs trazem os nomes dos usuários na mesma
         * consulta e evitam uma chamada adicional ao Identity
         * para cada linha retornada.
         *
         * A coluna bytea FileContent.Content NÃO participa da
         * projeção e, portanto, não é carregada.
         */
        var query =
            from attachment in
                _context.EntityAttachments.AsNoTracking()

            join createdUser in
                _context.Users.AsNoTracking()
                on attachment.CreatedBy
                equals (long?)createdUser.Id
                into createdUsers

            from createdUser in
                createdUsers.DefaultIfEmpty()

            join modifiedUser in
                _context.Users.AsNoTracking()
                on attachment.LastModifiedBy
                equals (long?)modifiedUser.Id
                into modifiedUsers

            from modifiedUser in
                modifiedUsers.DefaultIfEmpty()

            where
                attachment.EntityMetadataId == metadataId &&
                attachment.RecordId == recordId

            orderby
                attachment.Created descending,
                attachment.Id descending

            select new EntityAttachmentDto
            {
                Id =
                    attachment.Id,

                FileName =
                    attachment.FileContent.FileName,

                ContentType =
                    attachment.FileContent.ContentType,

                Extension =
                    attachment.FileContent.Extension,

                Size =
                    attachment.FileContent.Size,

                Title =
                    attachment.Title,

                Description =
                    attachment.Description,

                ReferenceDate =
                    attachment.ReferenceDate,

                Created =
                    attachment.Created,

                CreatedBy =
                    attachment.CreatedBy,

                CreatedByName =
                    createdUser != null
                        ? createdUser.FullName
                        : null,

                LastModified =
                    attachment.LastModified,

                LastModifiedBy =
                    attachment.LastModifiedBy,

                LastModifiedByName =
                    modifiedUser != null
                        ? modifiedUser.FullName
                        : null,

                Version =
                    EF.Property<uint>(
                        attachment,
                        "Version")
            };

        return await query.ToArrayAsync(
            cancellationToken);
    }

    public async Task<EntityAttachmentContentDto>
        GetContentAsync(
            long attachmentId,
            CancellationToken cancellationToken = default)
    {
        if (attachmentId <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(attachmentId),
                "O identificador do anexo deve ser maior que zero.");
        }

        /*
         * Este é o fluxo normal em que o conteúdo binário é
         * carregado intencionalmente.
         */
        return await _context.EntityAttachments
            .AsNoTracking()
            .Where(attachment =>
                attachment.Id == attachmentId)
            .Select(attachment =>
                new EntityAttachmentContentDto
                {
                    FileName =
                        attachment.FileContent.FileName,

                    ContentType =
                        attachment.FileContent.ContentType,

                    Content =
                        attachment.FileContent.Content
                })
            .SingleOrDefaultAsync(
                cancellationToken)
            ?? throw new KeyNotFoundException(
                $"Anexo '{attachmentId}' não foi encontrado.");
    }

    public async Task<EntityAttachmentDto>
        UpdateDetailsAsync(
            long attachmentId,
            uint version,
            string? title,
            string? description,
            DateTimeOffset? referenceDate,
            CancellationToken cancellationToken = default)
    {
        if (attachmentId <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(attachmentId),
                "O identificador do anexo deve ser maior que zero.");
        }

        if (version == 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(version),
                "A versão do anexo é obrigatória.");
        }

        var attachment =
            await _context.EntityAttachments
                .SingleOrDefaultAsync(
                    attachment =>
                        attachment.Id == attachmentId,
                    cancellationToken)
            ?? throw new KeyNotFoundException(
                $"Anexo '{attachmentId}' não foi encontrado.");

        /*
         * O valor recebido pelo cliente representa o xmin que ele
         * conhecia no momento em que carregou o registro.
         *
         * O EF Core o utilizará como OriginalValue na cláusula
         * de concorrência do UPDATE.
         */
        _context.Entry(attachment)
            .Property<uint>("Version")
            .OriginalValue = version;

        attachment.UpdateDetails(
            title,
            description,
            referenceDate);

        /*
         * Se outro usuário tiver alterado ou excluído a linha
         * depois que o cliente obteve a Version enviada, o
         * PostgreSQL não encontrará a linha com aquele xmin e o
         * EF Core gerará DbUpdateConcurrencyException.
         *
         * O tratamento global de concorrência do Atlas continua
         * sendo responsável por converter essa condição para a
         * resposta padronizada.
         */
        await _context.SaveChangesAsync(
            cancellationToken);

        /*
         * O xmin muda após o UPDATE. Esta consulta devolve ao
         * frontend a nova Version, necessária para a próxima
         * alteração ou exclusão.
         */
        return await GetAttachmentDtoAsync(
            attachment.Id,
            cancellationToken);
    }

    public async Task<DeleteEntityAttachmentResponse>
        DeleteAsync(
            long attachmentId,
            uint version,
            CancellationToken cancellationToken = default)
    {
        if (attachmentId <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(attachmentId),
                "O identificador do anexo deve ser maior que zero.");
        }

        if (version == 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(version),
                "A versão do anexo é obrigatória.");
        }

        await using var transaction =
            await _context.Database.BeginTransactionAsync(
                cancellationToken);

        try
        {
            var attachment =
                await _context.EntityAttachments
                    .SingleOrDefaultAsync(
                        attachment =>
                            attachment.Id == attachmentId,
                        cancellationToken)
                ?? throw new KeyNotFoundException(
                    $"Anexo '{attachmentId}' não foi encontrado.");

            var fileContentId =
                attachment.FileContentId;

            /*
             * A Version informada pelo frontend passa a ser o
             * OriginalValue da shadow property de concorrência.
             *
             * O DELETE emitido pelo EF considerará Id + xmin.
             */
            _context.Entry(attachment)
                .Property<uint>("Version")
                .OriginalValue = version;

            _context.EntityAttachments.Remove(
                attachment);

            /*
             * Caso o registro tenha sido alterado ou excluído
             * concorrentemente entre a leitura e este DELETE,
             * SaveChangesAsync gera DbUpdateConcurrencyException.
             */
            await _context.SaveChangesAsync(
                cancellationToken);

            /*
             * FileContent não deve sobreviver sem nenhum
             * consumidor quando foi criado exclusivamente para
             * este anexo.
             *
             * A consulta também mantém a implementação preparada
             * para reutilização futura do mesmo FileContent por
             * outro vínculo.
             */
            var hasRemainingReferences =
                await _context.EntityAttachments
                    .AsNoTracking()
                    .AnyAsync(
                        attachment =>
                            attachment.FileContentId ==
                            fileContentId,
                        cancellationToken);

            if (!hasRemainingReferences)
            {
                var fileContent =
                    await _context.FileContents
                        .SingleOrDefaultAsync(
                            file =>
                                file.Id == fileContentId,
                            cancellationToken);

                if (fileContent is not null)
                {
                    _context.FileContents.Remove(
                        fileContent);

                    await _context.SaveChangesAsync(
                        cancellationToken);
                }
            }

            await transaction.CommitAsync(
                cancellationToken);

            return new DeleteEntityAttachmentResponse(
                attachmentId,
                version);
        }
        catch
        {
            await transaction.RollbackAsync(
                cancellationToken);

            throw;
        }
    }

    private async Task<EntityAttachmentDto>
        GetAttachmentDtoAsync(
            long attachmentId,
            CancellationToken cancellationToken)
    {
        var query =
            from attachment in
                _context.EntityAttachments.AsNoTracking()

            join createdUser in
                _context.Users.AsNoTracking()
                on attachment.CreatedBy
                equals (long?)createdUser.Id
                into createdUsers

            from createdUser in
                createdUsers.DefaultIfEmpty()

            join modifiedUser in
                _context.Users.AsNoTracking()
                on attachment.LastModifiedBy
                equals (long?)modifiedUser.Id
                into modifiedUsers

            from modifiedUser in
                modifiedUsers.DefaultIfEmpty()

            where attachment.Id == attachmentId

            select new EntityAttachmentDto
            {
                Id =
                    attachment.Id,

                FileName =
                    attachment.FileContent.FileName,

                ContentType =
                    attachment.FileContent.ContentType,

                Extension =
                    attachment.FileContent.Extension,

                Size =
                    attachment.FileContent.Size,

                Title =
                    attachment.Title,

                Description =
                    attachment.Description,

                ReferenceDate =
                    attachment.ReferenceDate,

                Created =
                    attachment.Created,

                CreatedBy =
                    attachment.CreatedBy,

                CreatedByName =
                    createdUser != null
                        ? createdUser.FullName
                        : null,

                LastModified =
                    attachment.LastModified,

                LastModifiedBy =
                    attachment.LastModifiedBy,

                LastModifiedByName =
                    modifiedUser != null
                        ? modifiedUser.FullName
                        : null,

                Version =
                    EF.Property<uint>(
                        attachment,
                        "Version")
            };

        return await query.SingleAsync(
            cancellationToken);
    }

    private static string ComputeHash(
        byte[] content)
    {
        var hash =
            SHA256.HashData(content);

        return Convert
            .ToHexString(hash)
            .ToLowerInvariant();
    }

    private static string ResolveExtension(
        string fileName)
    {
        var extension =
            Path.GetExtension(fileName);

        if (string.IsNullOrWhiteSpace(extension))
        {
            throw new ArgumentException(
                $"O arquivo '{fileName}' não possui uma extensão válida.",
                nameof(fileName));
        }

        return extension;
    }
}

namespace Atlas.Application.Common.Interfaces;
public interface IIconInitializer
{
    Task EnsureIconsAsync(long? adminId, CancellationToken cancellationToken = default);
}

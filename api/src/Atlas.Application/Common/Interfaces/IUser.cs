namespace Atlas.Application.Common.Interfaces;

public interface IUser
{
    long? Id { get; }
    List<string>? Roles { get; }
}

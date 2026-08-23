namespace KaamLink.Application.Abstractions;

/// <summary>The authenticated caller, resolved from the JWT by the Api host.</summary>
public interface ICurrentUser
{
    Guid UserId { get; }
}

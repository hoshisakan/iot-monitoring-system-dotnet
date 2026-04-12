using MediatR;

namespace Pico2WH.Pi5.IIoT.Application.Behaviors;

/// <summary>
/// 可選：角色／範圍前置檢查占位。實際授權仍以 Api JWT middleware 為主；此處預設直通。
/// </summary>
public sealed class AuthorizationBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    public Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken) =>
        next();
}

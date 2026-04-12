/**
 * Marker type for application-layer use cases (clean architecture).
 * Concrete use cases are async functions or classes in `monitoring/application`.
 */
export type AsyncUseCase<Input, Output> = (input: Input) => Promise<Output>

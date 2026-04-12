export class UnauthorizedError extends Error {
  readonly name = 'UnauthorizedError'
  constructor(message = 'Unauthorized') {
    super(message)
  }
}

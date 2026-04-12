/** 後端回傳 403（例如 AdminOnly 政策）。 */
export class ForbiddenError extends Error {
  constructor(message = 'Forbidden') {
    super(message)
    this.name = 'ForbiddenError'
  }
}

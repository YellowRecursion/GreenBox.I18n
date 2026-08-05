export async function getJson<T>(path: string, signal?: AbortSignal): Promise<T> {
  return requestJson<T>(path, { signal })
}

export async function postJson<TResponse>(path: string, body: unknown): Promise<TResponse> {
  return requestJson<TResponse>(path, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(body),
  })
}

export class HttpError extends Error {
  readonly status: number
  readonly code?: string

  constructor(
    message: string,
    status: number,
    code?: string,
  ) {
    super(message)
    this.name = 'HttpError'
    this.status = status
    this.code = code
  }
}

async function requestJson<T>(path: string, init: RequestInit): Promise<T> {
  const response = await fetch(path, init)

  if (!response.ok) {
    const error = await readError(response)
    throw new HttpError(
      error?.message ?? `Request failed with status ${response.status}.`,
      response.status,
      error?.code,
    )
  }

  return response.json() as Promise<T>
}

async function readError(response: Response): Promise<{ code?: string; message?: string } | undefined> {
  try {
    return (await response.json()) as { code?: string; message?: string }
  } catch {
    return undefined
  }
}

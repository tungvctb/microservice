import axios, {
  AxiosError,
  type AxiosInstance,
  type AxiosRequestConfig,
  type InternalAxiosRequestConfig,
} from 'axios'
import type { ApiResponse, AuthResult } from './types'

const BASE_URL = import.meta.env.VITE_API_BASE_URL ?? '/api'

const ACCESS_TOKEN_KEY = 'ecom.accessToken'
const REFRESH_TOKEN_KEY = 'ecom.refreshToken'

export const tokenStore = {
  get access() {
    return localStorage.getItem(ACCESS_TOKEN_KEY)
  },
  get refresh() {
    return localStorage.getItem(REFRESH_TOKEN_KEY)
  },
  save(auth: Pick<AuthResult, 'accessToken' | 'refreshToken'>) {
    localStorage.setItem(ACCESS_TOKEN_KEY, auth.accessToken)
    localStorage.setItem(REFRESH_TOKEN_KEY, auth.refreshToken)
  },
  clear() {
    localStorage.removeItem(ACCESS_TOKEN_KEY)
    localStorage.removeItem(REFRESH_TOKEN_KEY)
  },
}

/** Lỗi nghiệp vụ đã bóc khỏi envelope — UI chỉ cần đọc message/code. */
export class ApiException extends Error {
  constructor(
    public readonly code: string,
    message: string,
    public readonly details?: string[],
    public readonly status?: number,
  ) {
    super(message)
    this.name = 'ApiException'
  }
}

export const http: AxiosInstance = axios.create({
  baseURL: BASE_URL,
  timeout: 20_000,
  headers: { 'Content-Type': 'application/json' },
})

http.interceptors.request.use((config: InternalAxiosRequestConfig) => {
  const token = tokenStore.access
  if (token) config.headers.Authorization = `Bearer ${token}`
  return config
})

// --- Tự làm mới access token khi gặp 401 ---------------------------------
// Nhiều request 401 cùng lúc chỉ kích hoạt ĐÚNG MỘT lần gọi /auth/refresh;
// các request còn lại xếp hàng chờ token mới rồi phát lại.
let refreshPromise: Promise<string> | null = null

const onUnauthorized = () => {
  tokenStore.clear()
  if (!location.pathname.startsWith('/login')) {
    location.href = `/login?redirect=${encodeURIComponent(location.pathname)}`
  }
}

async function refreshAccessToken(): Promise<string> {
  const refreshToken = tokenStore.refresh
  if (!refreshToken) throw new ApiException('auth.no_refresh_token', 'Phiên đăng nhập đã hết hạn.')

  const { data } = await axios.post<ApiResponse<AuthResult>>(
    `${BASE_URL}/auth/refresh`,
    { refreshToken },
    { headers: { 'Content-Type': 'application/json' } },
  )

  if (!data.success || !data.data) {
    throw new ApiException(data.error?.code ?? 'auth.refresh_failed', 'Không làm mới được phiên.')
  }

  tokenStore.save(data.data)
  return data.data.accessToken
}

http.interceptors.response.use(
  (response) => response,
  async (error: AxiosError<ApiResponse<unknown>>) => {
    const original = error.config as (AxiosRequestConfig & { _retried?: boolean }) | undefined
    const status = error.response?.status

    const isRefreshCall = original?.url?.includes('/auth/refresh')

    if (status === 401 && original && !original._retried && !isRefreshCall && tokenStore.refresh) {
      original._retried = true
      try {
        refreshPromise ??= refreshAccessToken().finally(() => {
          refreshPromise = null
        })
        const token = await refreshPromise

        original.headers = { ...original.headers, Authorization: `Bearer ${token}` }
        return http.request(original)
      } catch {
        onUnauthorized()
      }
    }

    if (status === 401) onUnauthorized()

    const payload = error.response?.data
    throw new ApiException(
      payload?.error?.code ?? 'network.error',
      payload?.error?.message ?? error.message ?? 'Không kết nối được máy chủ.',
      payload?.error?.details,
      status,
    )
  },
)

/** Bóc envelope ApiResponse, ném ApiException nếu service báo lỗi. */
async function unwrap<T>(promise: Promise<{ data: ApiResponse<T> }>): Promise<T> {
  const { data } = await promise
  if (!data.success) {
    throw new ApiException(
      data.error?.code ?? 'unknown',
      data.error?.message ?? 'Yêu cầu thất bại.',
      data.error?.details,
    )
  }
  return data.data as T
}

export const api = {
  get: <T>(url: string, config?: AxiosRequestConfig) => unwrap<T>(http.get(url, config)),
  post: <T>(url: string, body?: unknown, config?: AxiosRequestConfig) =>
    unwrap<T>(http.post(url, body, config)),
  put: <T>(url: string, body?: unknown) => unwrap<T>(http.put(url, body)),
  patch: <T>(url: string, body?: unknown) => unwrap<T>(http.patch(url, body)),
  /** DELETE trả 204 không có body — không bóc envelope. */
  delete: async (url: string): Promise<void> => {
    await http.delete(url)
  },
}

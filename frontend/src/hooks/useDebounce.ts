import { useEffect, useState } from 'react'

/** Hoãn giá trị lại để không gọi API sau mỗi ký tự gõ vào ô tìm kiếm. */
export function useDebounce<T>(value: T, delayMs = 400): T {
  const [debounced, setDebounced] = useState(value)

  useEffect(() => {
    const timer = setTimeout(() => setDebounced(value), delayMs)
    return () => clearTimeout(timer)
  }, [value, delayMs])

  return debounced
}

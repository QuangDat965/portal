import { useQuery } from '@tanstack/react-query'
import { Navigate, Outlet, useLocation } from 'react-router-dom'
import { api, ApiError, type Me } from '../lib/api'
import { Spinner } from './ui'

export function useMe() {
  return useQuery<Me>({
    queryKey: ['me'],
    queryFn: api.me,
    retry: false,
    staleTime: 30_000,
  })
}

export function RequireAuth() {
  const location = useLocation()
  const { data, isLoading, error } = useMe()

  if (isLoading) {
    return <Spinner label="Đang kiểm tra phiên đăng nhập…" />
  }

  if (error) {
    if (error instanceof ApiError && error.status === 401) {
      return <Navigate to="/login" replace state={{ from: location.pathname }} />
    }

    return (
      <div className="mx-auto mt-20 max-w-md rounded-xl border border-rose-500/30 bg-rose-500/10 p-6 text-center text-sm text-rose-200">
        Không kết nối được API. Kiểm tra backend rồi thử lại.
      </div>
    )
  }

  if (!data) {
    return <Navigate to="/login" replace />
  }

  return <Outlet />
}

export function RequireAdmin() {
  const { data } = useMe()

  if (!data?.roles.includes('admin')) {
    return <Navigate to="/apps" replace />
  }

  return <Outlet />
}

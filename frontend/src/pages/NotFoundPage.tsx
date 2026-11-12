import { Link } from 'react-router-dom'

export function NotFoundPage() {
  return (
    <div className="flex min-h-screen flex-col items-center justify-center gap-4 text-center">
      <p className="text-5xl font-black text-white">404</p>
      <p className="text-sm text-slate-400">Trang không tồn tại.</p>
      <Link
        to="/apps"
        className="rounded-lg bg-sky-500 px-4 py-2 text-sm font-semibold text-slate-950 hover:bg-sky-400"
      >
        Về trang ứng dụng
      </Link>
    </div>
  )
}

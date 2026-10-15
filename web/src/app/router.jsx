import { createBrowserRouter } from 'react-router'
import { StoreLayout } from './layouts/StoreLayout'
import { HomePage } from '../pages/HomePage'
import { NotFoundPage, RouteErrorPage } from '../pages/ErrorPages'

export const routes = [
  {
    element: <StoreLayout />,
    errorElement: <RouteErrorPage />,
    children: [
      { index: true, element: <HomePage /> },
      { path: '*', element: <NotFoundPage /> },
    ],
  },
]

export const router = createBrowserRouter(routes)

import { createBrowserRouter } from 'react-router'
import { StoreLayout } from './layouts/StoreLayout'
import { HomePage } from '../pages/HomePage'
import { NotFoundPage, RouteErrorPage } from '../pages/ErrorPages'
import { RequireAuth } from '../features/auth/RequireAuth'
import { CategoryPage, ProductPage, SearchPage } from '../features/catalog/CatalogPages'

// Storefront pages ship in the main bundle. Account, sign-in and admin pages (and the form libraries
// they use) are downloaded on first visit, keeping the initial load small on mobile data.
const lazyPage = (load, name) => async () => ({ Component: (await load())[name] })

export const routes = [
  {
    element: <StoreLayout />,
    errorElement: <RouteErrorPage />,
    children: [
      { index: true, element: <HomePage /> },
      { path: 'category/:slug', element: <CategoryPage /> },
      { path: 'search', element: <SearchPage /> },
      { path: 'products/:id/:slug?', element: <ProductPage /> },
      { path: 'login', lazy: lazyPage(() => import('../features/auth/LoginPage'), 'LoginPage') },
      { path: 'register', lazy: lazyPage(() => import('../features/auth/RegisterPage'), 'RegisterPage') },
      {
        element: <RequireAuth />,
        children: [
          {
            path: 'account',
            lazy: lazyPage(() => import('../features/account/AccountLayout'), 'AccountLayout'),
            children: [
              { index: true, lazy: lazyPage(() => import('../features/account/ProfilePage'), 'ProfilePage') },
              {
                path: 'addresses',
                lazy: lazyPage(() => import('../features/account/AddressesPage'), 'AddressesPage'),
              },
              {
                path: 'addresses/new',
                lazy: lazyPage(() => import('../features/account/AddressFormPage'), 'AddressFormPage'),
              },
              {
                path: 'addresses/:id/edit',
                lazy: lazyPage(() => import('../features/account/AddressFormPage'), 'AddressFormPage'),
              },
            ],
          },
        ],
      },
      {
        path: 'admin',
        element: <RequireAuth role="Admin" />,
        children: [
          {
            lazy: lazyPage(() => import('../features/admin/AdminLayout'), 'AdminLayout'),
            children: [
              {
                index: true,
                lazy: lazyPage(() => import('../features/admin/AdminProductsPage'), 'AdminProductsPage'),
              },
              {
                path: 'products',
                lazy: lazyPage(() => import('../features/admin/AdminProductsPage'), 'AdminProductsPage'),
              },
              {
                path: 'products/new',
                lazy: lazyPage(() => import('../features/admin/AdminProductFormPage'), 'AdminProductFormPage'),
              },
              {
                path: 'products/:id',
                lazy: lazyPage(() => import('../features/admin/AdminProductFormPage'), 'AdminProductFormPage'),
              },
              {
                path: 'categories',
                lazy: lazyPage(() => import('../features/admin/AdminCategoriesPage'), 'AdminCategoriesPage'),
              },
              {
                path: 'customers',
                lazy: lazyPage(() => import('../features/admin/AdminCustomersPage'), 'AdminCustomersPage'),
              },
            ],
          },
        ],
      },
      { path: '*', element: <NotFoundPage /> },
    ],
  },
]

export const router = createBrowserRouter(routes)

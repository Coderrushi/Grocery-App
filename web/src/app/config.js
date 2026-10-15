/** Build-time settings from .env files (only VITE_-prefixed variables reach the browser). */
export const config = {
  storeName: import.meta.env.VITE_STORE_NAME || 'QuickKirana',
}

/** Only allow same-site paths, so a crafted link can't bounce users to another website after login. */
export function safeReturnPath(value) {
  return value && value.startsWith('/') && !value.startsWith('//') && !value.startsWith('/\\') ? value : '/'
}

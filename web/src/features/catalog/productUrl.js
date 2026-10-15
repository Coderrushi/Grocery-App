/** "/products/42/amul-taaza-toned-milk". The id finds the product; the name part is only for people and SEO. */
export function productUrl(product) {
  const slug = product.name
    .normalize('NFD')
    .replace(/[̀-ͯ]/g, '')
    .toLowerCase()
    .replace(/[^a-z0-9]+/g, '-')
    .replace(/^-|-$/g, '')
  return slug ? `/products/${product.id}/${slug}` : `/products/${product.id}`
}

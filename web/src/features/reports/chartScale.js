/** Rounds an axis maximum up to a clean step: 0 / 500 / 1,000… or 0 / 2K / 4K… (about four steps). */
export function niceTicks(max, steps = 4) {
  if (!(max > 0)) return [0, 100]
  const raw = max / steps
  const power = 10 ** Math.floor(Math.log10(raw))
  const step = [1, 2, 5, 10].find((m) => raw <= m * power) * power
  return Array.from({ length: Math.ceil(max / step) + 1 }, (_, i) => i * step)
}

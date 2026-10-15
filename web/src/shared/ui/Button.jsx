const variants = {
  primary: 'bg-brand-600 text-white hover:bg-brand-700 disabled:bg-brand-600/50',
  secondary: 'border border-line bg-surface text-ink hover:bg-canvas disabled:text-ink-muted',
  danger: 'bg-danger-600 text-white hover:bg-danger-700 disabled:bg-danger-600/50',
  ghost: 'text-brand-700 hover:bg-brand-50',
}

const sizes = {
  sm: 'h-9 px-3 text-sm',
  md: 'h-11 px-4 text-base', // 44px: comfortable touch target on phones
  lg: 'h-12 px-6 text-base',
}

export function Button({ variant = 'primary', size = 'md', className = '', type = 'button', ...props }) {
  return (
    <button
      type={type}
      className={`inline-flex items-center justify-center gap-2 rounded-lg font-semibold transition-colors disabled:cursor-not-allowed ${variants[variant]} ${sizes[size]} ${className}`}
      {...props}
    />
  )
}

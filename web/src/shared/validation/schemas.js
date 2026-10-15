import { z } from 'zod'
import { MOBILE_PATTERN, PINCODE_PATTERN, normalizeIndianMobile } from '../utils/india'

// Field rules shared by several forms. They mirror the API's FluentValidation rules so most mistakes
// are caught instantly in the browser; the API remains the authority and re-checks everything.

export const nameField = (message = 'Enter your name.') => z.string().trim().min(1, message).max(100)

export const emailField = z
  .string()
  .trim()
  .min(1, 'Enter your email address.')
  .pipe(z.email('Enter a valid email address.'))

/** Accepts "+91 98765 43210" etc. and submits the normalised 10 digits. */
export const mobileField = z
  .string()
  .trim()
  .min(1, 'Enter your mobile number.')
  .transform(normalizeIndianMobile)
  .pipe(z.string().regex(MOBILE_PATTERN, 'Enter a valid 10-digit mobile number.'))

export const newPasswordField = z.string().min(8, 'Use at least 8 characters.').max(100)

export const pincodeField = z.string().trim().regex(PINCODE_PATTERN, 'Enter a valid 6-digit PIN code.')

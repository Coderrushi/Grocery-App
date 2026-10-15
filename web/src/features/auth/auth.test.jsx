import { screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it } from 'vitest'
import { anonymous, customer, mockApi, renderApp, signedInAs } from '../../test/renderApp'
import { safeReturnPath } from './safeReturnPath'

describe('protected routes', () => {
  it('send anonymous visitors to sign in, remembering where they were going', async () => {
    mockApi(anonymous)
    const { router } = renderApp('/account/addresses')

    expect(await screen.findByRole('heading', { name: 'Sign in' })).toBeInTheDocument()
    expect(router.state.location.search).toBe('?returnTo=%2Faccount%2Faddresses')
  })

  it('show access denied to customers on admin pages', async () => {
    mockApi(signedInAs(customer))
    renderApp('/admin/customers')

    expect(await screen.findByText('Access denied')).toBeInTheDocument()
  })
})

describe('sign in', () => {
  it('signs in and continues to the page the user asked for', async () => {
    const user = userEvent.setup()
    const calls = mockApi({
      ...anonymous,
      'POST /auth/login': () => [200, customer],
      'GET /account/addresses': () => [200, []],
    })
    const { router } = renderApp('/login?returnTo=%2Faccount%2Faddresses')

    await user.type(await screen.findByLabelText('Email or mobile number'), '98765 43210')
    await user.type(screen.getByLabelText('Password'), 'secret-password')
    await user.click(screen.getByRole('button', { name: 'Sign in' }))

    await waitFor(() => expect(router.state.location.pathname).toBe('/account/addresses'))
    expect(calls.find((c) => c.path === '/auth/login').body).toEqual({
      identifier: '98765 43210', // the API normalises the login identifier itself
      password: 'secret-password',
      rememberMe: true,
    })
    expect(await screen.findByRole('link', { name: /my account \(asha verma\)/i })).toBeInTheDocument()
  })

  it('shows the API message when credentials are wrong', async () => {
    const user = userEvent.setup()
    mockApi({
      ...anonymous,
      'POST /auth/login': () => [
        401,
        { status: 401, title: 'Sign-in failed', detail: 'The email/mobile number or password is incorrect.' },
      ],
    })
    renderApp('/login')

    await user.type(await screen.findByLabelText('Email or mobile number'), 'asha@example.com')
    await user.type(screen.getByLabelText('Password'), 'wrong')
    await user.click(screen.getByRole('button', { name: 'Sign in' }))

    expect(await screen.findByRole('alert')).toHaveTextContent('The email/mobile number or password is incorrect.')
  })
})

describe('registration', () => {
  it('validates in the browser before calling the API', async () => {
    const user = userEvent.setup()
    const calls = mockApi(anonymous)
    renderApp('/register')

    await user.click(await screen.findByRole('button', { name: 'Create account' }))

    expect(await screen.findByText('Enter your name.')).toBeInTheDocument()
    expect(screen.getByText('Enter your mobile number.')).toBeInTheDocument()
    expect(screen.getByLabelText('Full name')).toHaveAttribute('aria-invalid', 'true')
    expect(calls.some((c) => c.path === '/auth/register')).toBe(false)
  })

  it('sends a normalised mobile number and shows server field errors under the field', async () => {
    const user = userEvent.setup()
    const calls = mockApi({
      ...anonymous,
      'POST /auth/register': () => [
        400,
        {
          status: 400,
          title: 'Validation failed',
          errors: { password: ['Passwords must use at least 4 different characters.'] },
        },
      ],
    })
    renderApp('/register')

    await user.type(await screen.findByLabelText('Full name'), 'Asha Verma')
    await user.type(screen.getByLabelText('Mobile number'), '+91 98765 43210')
    await user.type(screen.getByLabelText('Email'), 'asha@example.com')
    await user.type(screen.getByLabelText('Password'), 'aaaaaaaa')
    await user.click(screen.getByRole('button', { name: 'Create account' }))

    expect(await screen.findByText('Passwords must use at least 4 different characters.')).toBeInTheDocument()
    expect(calls.find((c) => c.path === '/auth/register').body.phoneNumber).toBe('9876543210')
  })
})

describe('safeReturnPath', () => {
  it.each([
    ['/account', '/account'],
    ['https://evil.example', '/'],
    ['//evil.example', '/'],
    ['/\\evil.example', '/'],
    [null, '/'],
  ])('%s -> %s', (input, expected) => {
    expect(safeReturnPath(input)).toBe(expected)
  })
})

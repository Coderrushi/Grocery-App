import { screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it } from 'vitest'
import { customer, mockApi, renderApp, signedInAs } from '../../test/renderApp'

const savedAddress = {
  id: 11,
  label: 'Home',
  recipientName: 'Asha Verma',
  phoneNumber: '9876543210',
  line1: 'Flat 4B, Shanti Apartments',
  line2: 'MG Road',
  landmark: null,
  city: 'Pune',
  state: 'Maharashtra',
  pincode: '411038',
  isDefault: true,
}

describe('addresses', () => {
  it('lists saved addresses with the default badge', async () => {
    mockApi({ ...signedInAs(customer), 'GET /account/addresses': () => [200, [savedAddress]] })
    renderApp('/account/addresses')

    const card = (await screen.findByText('Flat 4B, Shanti Apartments, MG Road, Pune, Maharashtra 411038')).closest(
      'li',
    )
    expect(within(card).getByText('Default')).toBeInTheDocument()
    expect(within(card).queryByRole('button', { name: 'Set as default' })).not.toBeInTheDocument()
  })

  it('pre-fills the recipient, validates the PIN code, then saves', async () => {
    const user = userEvent.setup()
    const calls = mockApi({
      ...signedInAs(customer),
      'POST /account/addresses': (body) => [201, { id: 12, ...body }],
      'GET /account/addresses': () => [200, []],
    })
    const { router } = renderApp('/account/addresses/new')

    expect(await screen.findByLabelText('Recipient name')).toHaveValue('Asha Verma')
    await user.type(screen.getByLabelText('Flat / house no., building'), '12 Lake View')
    await user.type(screen.getByLabelText('City'), 'Pune')
    await user.selectOptions(screen.getByLabelText('State'), 'Maharashtra')
    await user.type(screen.getByLabelText('PIN code'), '01103')
    await user.click(screen.getByRole('button', { name: 'Add address' }))

    expect(await screen.findByText('Enter a valid 6-digit PIN code.')).toBeInTheDocument()

    await user.clear(screen.getByLabelText('PIN code'))
    await user.type(screen.getByLabelText('PIN code'), '411001')
    await user.click(screen.getByRole('button', { name: 'Add address' }))

    await waitFor(() => expect(router.state.location.pathname).toBe('/account/addresses'))
    expect(calls.find((c) => c.method === 'POST').body).toMatchObject({
      label: 'Home',
      line1: '12 Lake View',
      line2: null, // blank optional fields are sent as null
      pincode: '411001',
      phoneNumber: '9876543210',
    })
  })
})

export const origin = 'https://dexter-onboardflow.pages.dev';
export const previousOrigin = 'https://dexter-onboardflow.de.deplexo.com';

export function checkedOrigin(value = origin) {
  if (![origin, previousOrigin].includes(value)) throw new Error('Unexpected demo target.');
  return value;
}

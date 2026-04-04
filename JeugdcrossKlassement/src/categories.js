export const CATEGORY_ORDER = [
  'Mannen U20',
  'Mannen U18',
  'Vrouwen U20',
  'Vrouwen U18',
  'Jongens U16',
  'Meisjes U16',
  'Jongens U14',
  'Meisjes U14',
  'Jongens U13',
  'Meisjes U13',
  'Jongens U8',
  'Meisjes U8',
  'Jongens U9',
  'Meisjes U9',
  'Jongens U10',
  'Meisjes U10',
  'Jongens U11',
  'Meisjes U11',
  'Jongens U12',
  'Meisjes U12',
]

export function categoryFromAgeAndGender(age, gender) {
  if (!age || !gender) {
    return null
  }

  const isSenior = age >= 18
  const prefix = gender === 'male'
    ? isSenior ? 'Mannen' : 'Jongens'
    : isSenior ? 'Vrouwen' : 'Meisjes'

  return `${prefix} U${age}`
}

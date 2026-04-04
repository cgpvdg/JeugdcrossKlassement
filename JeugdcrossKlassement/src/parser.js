import { CATEGORY_ORDER, categoryFromAgeAndGender } from './categories.js'

function normalizeLine(value) {
  return value
    .replace(/\u00a0/g, ' ')
    .replace(/\s+/g, ' ')
    .trim()
}

export function normalizeParticipantKey(value) {
  return normalizeLine(value)
    .toLowerCase()
    .normalize('NFD')
    .replace(/[\u0300-\u036f]/g, '')
    .replace(/[^a-z0-9]+/g, ' ')
    .trim()
}

function detectGender(upperLine) {
  if (upperLine.includes('VROUWEN') || upperLine.includes('MEISJES')) {
    return 'female'
  }

  if (upperLine.includes('MANNEN') || upperLine.includes('JONGENS')) {
    return 'male'
  }

  if (upperLine.includes('-M,')) {
    return 'female'
  }

  if (upperLine.includes('-J,')) {
    return 'male'
  }

  if (/^\s*VU\d{1,2}/.test(upperLine)) {
    return 'female'
  }

  if (/^\s*MU\d{1,2}/.test(upperLine)) {
    return 'male'
  }

  return null
}

function detectAge(upperLine) {
  const leadingAgeMatch = upperLine.match(/^\s*[A-Z,\-\s]*U(20|18|16|14|13|12|11|10|9|8)\b/)
  if (leadingAgeMatch) {
    return Number.parseInt(leadingAgeMatch[1], 10)
  }

  const anyAgeMatch = upperLine.match(/U(20|18|16|14|13|12|11|10|9|8)\b/)
  if (anyAgeMatch) {
    return Number.parseInt(anyAgeMatch[1], 10)
  }

  return null
}

function parseCategoryHeader(rawLine) {
  const line = normalizeLine(rawLine)
  if (!line) {
    return null
  }

  const upperLine = line.toUpperCase()
  if (!upperLine.includes('U')) {
    return null
  }

  const age = detectAge(upperLine)
  const gender = detectGender(upperLine)
  const label = categoryFromAgeAndGender(age, gender)

  if (!label || !CATEGORY_ORDER.includes(label)) {
    return null
  }

  return label
}

function parseResultLine(rawLine) {
  const match = rawLine.match(/^\s*(\d+)\s+(.+?)(?:\s{2,}(.+?))?\s+(\d{1,2}:\d{2}(?::\d{2})?)\s*$/)
  if (!match) {
    return null
  }

  const rank = Number.parseInt(match[1], 10)
  const name = normalizeLine(match[2])
  const association = normalizeLine(match[3] || '')
  const time = normalizeLine(match[4])

  if (!rank || !name) {
    return null
  }

  return {
    rank,
    name,
    association,
    time,
    points: rank,
    participantKey: normalizeParticipantKey(name),
  }
}

export function parseCrossResults(rawText) {
  const lines = rawText.split(/\r?\n/)
  const parsed = []
  let currentCategory = null

  for (const rawLine of lines) {
    const categoryFromHeader = parseCategoryHeader(rawLine)
    if (categoryFromHeader) {
      currentCategory = categoryFromHeader
      continue
    }

    if (!currentCategory) {
      continue
    }

    const resultLine = parseResultLine(rawLine)
    if (!resultLine) {
      continue
    }

    parsed.push({
      ...resultLine,
      category: currentCategory,
    })
  }

  return parsed
}

export function decodeTextFileFromArrayBuffer(arrayBuffer) {
  const bytes = new Uint8Array(arrayBuffer)

  try {
    return new TextDecoder('utf-8', { fatal: true }).decode(bytes)
  }
  catch {
    return new TextDecoder('windows-1252').decode(bytes)
  }
}

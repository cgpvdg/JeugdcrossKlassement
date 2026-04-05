import { CATEGORY_ORDER, categoryFromAgeAndGender } from './categories.js'

const DUTCH_MONTHS = {
  januari: 1,
  februari: 2,
  maart: 3,
  april: 4,
  mei: 5,
  juni: 6,
  juli: 7,
  augustus: 8,
  september: 9,
  oktober: 10,
  november: 11,
  december: 12,
}

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
  if (upperLine.includes('VROUWEN') || upperLine.includes('MEISJES') || upperLine.includes('DAMES')) {
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

  if (/^\s*DU\d{1,2}/.test(upperLine)) {
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

  const knownAssociationKeys = new Set(
    parsed
      .map((row) => normalizeParticipantKey(row.association))
      .filter((value) => value.length > 0),
  )

  for (const row of parsed) {
    if (row.association) {
      continue
    }

    const nameParts = row.name.split(' ').filter((part) => part.length > 0)
    const maxTailSize = Math.min(4, nameParts.length - 1)
    let fixed = false

    for (let tailSize = maxTailSize; tailSize >= 1; tailSize -= 1) {
      const tail = nameParts.slice(-tailSize).join(' ')
      const tailKey = normalizeParticipantKey(tail)
      if (!knownAssociationKeys.has(tailKey)) {
        continue
      }

      const cleanedName = nameParts.slice(0, -tailSize).join(' ').trim()
      if (!cleanedName) {
        continue
      }

      row.association = tail
      row.name = cleanedName
      row.participantKey = normalizeParticipantKey(cleanedName)
      fixed = true
      break
    }

    if (!fixed) {
      row.association = ''
    }
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

function parseDateFromFreeText(value) {
  const normalized = normalizeLine(value).toLowerCase()
  const textualMatch = normalized.match(/(\d{1,2})\s+([a-zà-ÿ]+)\s+(\d{4})/)
  if (textualMatch) {
    const day = Number.parseInt(textualMatch[1], 10)
    const month = DUTCH_MONTHS[textualMatch[2]]
    const year = Number.parseInt(textualMatch[3], 10)
    if (month && day >= 1 && day <= 31) {
      const isoDate = `${year.toString().padStart(4, '0')}-${month.toString().padStart(2, '0')}-${day.toString().padStart(2, '0')}`
      return isoDate
    }
  }

  const numericMatch = normalized.match(/(\d{1,2})[-/](\d{1,2})[-/](\d{4})/)
  if (numericMatch) {
    const day = Number.parseInt(numericMatch[1], 10)
    const month = Number.parseInt(numericMatch[2], 10)
    const year = Number.parseInt(numericMatch[3], 10)
    const isoDate = `${year.toString().padStart(4, '0')}-${month.toString().padStart(2, '0')}-${day.toString().padStart(2, '0')}`
    return isoDate
  }

  return null
}

export function parseCrossMetadata(rawText) {
  const lines = rawText
    .split(/\r?\n/)
    .map((line) => normalizeLine(line))
    .filter((line) => line.length > 0)

  if (lines.length < 2) {
    throw new Error('Bestand bevat te weinig informatie voor wedstrijdmetadata.')
  }

  const firstLine = lines[0]
  if (!/^uitslagenlijst\b/i.test(firstLine)) {
    throw new Error('Kopregel "Uitslagenlijst ..." niet gevonden.')
  }

  const name = firstLine.replace(/^uitslagenlijst\s*/i, '').trim()
  if (!name) {
    throw new Error('Wedstrijdnaam ontbreekt in de kopregel.')
  }

  const secondLine = lines[1]
  const splitIndex = secondLine.indexOf(' - ')
  const association = splitIndex >= 0 ? secondLine.slice(0, splitIndex).trim() : secondLine
  const dateSource = splitIndex >= 0 ? secondLine.slice(splitIndex + 3) : secondLine
  const date = parseDateFromFreeText(dateSource)

  if (!association) {
    throw new Error('Vereniging/plaats kon niet bepaald worden.')
  }

  if (!date) {
    throw new Error('Datum kon niet bepaald worden uit de tweede regel.')
  }

  return {
    name,
    association,
    date,
  }
}

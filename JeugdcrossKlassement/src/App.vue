<script setup>
import { computed, markRaw, onMounted, ref, shallowRef } from 'vue'
import { CATEGORY_ORDER } from './categories.js'
import { getDatabase, resetDatabase } from './db.js'
import {
  decodeTextFileFromArrayBuffer,
  normalizeParticipantKey,
  parseCrossMetadata,
  parseCrossResults,
} from './parser.js'

const db = shallowRef(null)
const isLoading = ref(true)
const errorMessage = ref('')
const successMessage = ref('')
const activeTab = ref('wedstrijden')
const teamBreakdownModal = ref(null)
let notificationTimeoutId = null

const crosses = ref([])
const results = ref([])
const participantDecisions = ref([])
const crossAssociationDecisions = ref([])

function resetMessages() {
  if (notificationTimeoutId) {
    clearTimeout(notificationTimeoutId)
    notificationTimeoutId = null
  }
  errorMessage.value = ''
  successMessage.value = ''
}

function showSuccess(message) {
  resetMessages()
  successMessage.value = message
  notificationTimeoutId = setTimeout(() => {
    successMessage.value = ''
    notificationTimeoutId = null
  }, 5000)
}

function showError(message) {
  resetMessages()
  errorMessage.value = message
  notificationTimeoutId = setTimeout(() => {
    errorMessage.value = ''
    notificationTimeoutId = null
  }, 5000)
}

function formatDate(dateText) {
  const date = new Date(dateText)
  if (Number.isNaN(date.getTime())) {
    return dateText
  }
  return date.toLocaleDateString('nl-NL')
}

function sanitizeForId(value) {
  return value
    .normalize('NFD')
    .replace(/[\u0300-\u036f]/g, '')
    .replace(/[^a-zA-Z0-9]+/g, '-')
    .replace(/^-+|-+$/g, '')
    .toLowerCase()
}

function canonicalAssociationKey(value) {
  return normalizeParticipantKey(value || '')
}

function createGroupKey(category, association) {
  return `${category}::${canonicalAssociationKey(association)}`
}

function sortedPair(left, right) {
  return left < right ? [left, right] : [right, left]
}

function createDecisionId(category, associationKey, leftKey, rightKey) {
  const [a, b] = sortedPair(leftKey, rightKey)
  return `${sanitizeForId(category)}::${associationKey}::${a}::${b}`
}

function createCrossAssociationDecisionId(category, leftParticipantKey, leftAssociationKey, rightParticipantKey, rightAssociationKey) {
  const left = `${leftParticipantKey}::${leftAssociationKey}`
  const right = `${rightParticipantKey}::${rightAssociationKey}`
  const [a, b] = sortedPair(left, right)
  return `${sanitizeForId(category)}::cross-association::${a}::${b}`
}

function compactKey(value) {
  return (value || '').replace(/\s+/g, '')
}

function levenshteinDistance(left, right) {
  const a = compactKey(left)
  const b = compactKey(right)

  if (a === b) {
    return 0
  }

  if (a.length === 0) {
    return b.length
  }

  if (b.length === 0) {
    return a.length
  }

  const matrix = Array.from({ length: a.length + 1 }, () => Array(b.length + 1).fill(0))
  for (let i = 0; i <= a.length; i += 1) {
    matrix[i][0] = i
  }
  for (let j = 0; j <= b.length; j += 1) {
    matrix[0][j] = j
  }

  for (let i = 1; i <= a.length; i += 1) {
    for (let j = 1; j <= b.length; j += 1) {
      const cost = a[i - 1] === b[j - 1] ? 0 : 1
      matrix[i][j] = Math.min(
        matrix[i - 1][j] + 1,
        matrix[i][j - 1] + 1,
        matrix[i - 1][j - 1] + cost,
      )
    }
  }

  return matrix[a.length][b.length]
}

function similarity(left, right) {
  const a = compactKey(left)
  const b = compactKey(right)
  const maxLength = Math.max(a.length, b.length)
  if (maxLength === 0) {
    return 1
  }
  const distance = levenshteinDistance(a, b)
  return 1 - distance / maxLength
}

function baseFinalistsCount(classifiedCount) {
  if (classifiedCount <= 0) {
    return 0
  }

  if (classifiedCount <= 3) {
    return classifiedCount
  }

  if (classifiedCount === 4) {
    return 3
  }

  if (classifiedCount <= 6) {
    return 4
  }

  return Math.ceil(classifiedCount * 0.5)
}

const COMBINED_TEAM_CATEGORY_CONFIG = [
  {
    label: 'Mannen U20/U18',
    sourceCategories: ['Mannen U20', 'Mannen U18'],
  },
  {
    label: 'Vrouwen U20/U18',
    sourceCategories: ['Vrouwen U20', 'Vrouwen U18'],
  },
]

const TEAM_CATEGORY_CONFIG = [
  ...COMBINED_TEAM_CATEGORY_CONFIG,
  ...CATEGORY_ORDER
    .filter((category) => !['Mannen U20', 'Mannen U18', 'Vrouwen U20', 'Vrouwen U18'].includes(category))
    .map((category) => ({
      label: category,
      sourceCategories: [category],
    })),
]

function teamFinalistsCount(classifiedCount) {
  if (classifiedCount <= 0) {
    return 0
  }
  if (classifiedCount === 1) {
    return 1
  }
  if (classifiedCount <= 3) {
    return 2
  }
  if (classifiedCount === 4) {
    return 3
  }
  if (classifiedCount <= 6) {
    return 4
  }
  if (classifiedCount <= 8) {
    return 5
  }
  if (classifiedCount <= 10) {
    return 6
  }
  return 7
}

function isExcludedFromTeamQualification(associationKey) {
  return (
    associationKey === 'ntb'
    || associationKey.includes('nederlandse triathlon bon')
    || associationKey.includes('nederlandse triathlon bond')
  )
}

function openTeamBreakdown(row, categoryLabel, isCombinedCategory, cross, crossIndex) {
  const details = row.detailsPerCross?.[crossIndex] || null
  if (!details) {
    return
  }

  teamBreakdownModal.value = {
    categoryLabel,
    association: row.association,
    crossName: cross.name,
    crossDate: formatDate(cross.date),
    isCombinedCategory,
    score: row.pointsPerCross[crossIndex],
    participants: details.participants,
  }
}

function closeTeamBreakdown() {
  teamBreakdownModal.value = null
}

function participantDisplayName(participant, isCombinedCategory) {
  if (!isCombinedCategory) {
    return participant.name
  }

  const ageMatch = participant.sourceCategory?.match(/U(20|18)\b/)
  if (!ageMatch) {
    return participant.name
  }

  return `${participant.name} (U${ageMatch[1]})`
}

async function refreshData() {
  if (!db.value) {
    return
  }

  const crossDocs = await db.value.crosses.find().sort({ date: 'asc' }).exec()
  const resultDocs = await db.value.results.find().exec()
  const decisionDocs = await db.value.participantDecisions.find().exec()
  const crossAssociationDecisionDocs = await db.value.crossAssociationDecisions.find().exec()

  crosses.value = crossDocs.map((doc) => doc.toJSON())
  results.value = resultDocs.map((doc) => doc.toJSON())
  participantDecisions.value = decisionDocs.map((doc) => doc.toJSON())
  crossAssociationDecisions.value = crossAssociationDecisionDocs.map((doc) => doc.toJSON())
}

async function init() {
  try {
    db.value = markRaw(await getDatabase())
    await refreshData()
  }
  catch (error) {
    showError(`Initialisatie mislukt: ${error instanceof Error ? error.message : String(error)}`)
  }
  finally {
    isLoading.value = false
  }
}

async function resetAllData() {
  resetMessages()
  const shouldReset = window.confirm(
    'Weet je zeker dat je alles wilt verwijderen? Dit wist alle crossen, uitslagen, naamkeuzes en de lokale database.',
  )
  if (!shouldReset) {
    return
  }

  try {
    await resetDatabase()
    db.value = markRaw(await getDatabase())
    crosses.value = []
    results.value = []
    participantDecisions.value = []
    crossAssociationDecisions.value = []
    await refreshData()
    showSuccess('Alles is verwijderd. Je kunt nu schoon starten.')
  }
  catch (error) {
    showError(`Reset mislukt: ${error instanceof Error ? error.message : String(error)}`)
  }
}

async function removeCross(cross) {
  resetMessages()

  const shouldDelete = window.confirm(
    `Weet je zeker dat je "${cross.name}" volledig wilt verwijderen? Dit verwijdert ook alle geuploade uitslagen voor deze wedstrijd.`,
  )
  if (!shouldDelete) {
    return
  }

  const resultDocs = await db.value.results.find({ selector: { crossId: cross.id } }).exec()
  if (resultDocs.length > 0) {
    await db.value.results.bulkRemove(resultDocs.map((doc) => doc.id))
  }

  const crossDoc = await db.value.crosses.findOne({ selector: { id: cross.id } }).exec()
  if (crossDoc) {
    await crossDoc.remove()
  }

  await refreshData()
  showSuccess(`Wedstrijd "${cross.name}" verwijderd.`)
}

async function clearCrossResults(cross, showSuccessMessage = true) {
  const docs = await db.value.results.find({ selector: { crossId: cross.id } }).exec()
  if (docs.length === 0) {
    if (showSuccessMessage) {
      showSuccess(`Geen uitslag gevonden voor ${cross.name}.`)
    }
    return
  }

  await db.value.results.bulkRemove(docs.map((doc) => doc.id))
  await refreshData()

  if (showSuccessMessage) {
    showSuccess(`Uitslag verwijderd voor ${cross.name}.`)
  }
}

function normalizeCrossIdentity(value) {
  return normalizeParticipantKey(value || '').replace(/\s+/g, ' ')
}

function isSameCross(existingCross, metadata) {
  if (existingCross.date !== metadata.date) {
    return false
  }

  const sameAssociation
    = normalizeCrossIdentity(existingCross.association) === normalizeCrossIdentity(metadata.association)
  const sameName = normalizeCrossIdentity(existingCross.name) === normalizeCrossIdentity(metadata.name)

  return (
    sameAssociation || sameName
  )
}

async function onCompetitionFileSelected(event) {
  resetMessages()

  const file = event.target.files?.[0]
  if (!file) {
    return
  }

  try {
    const arrayBuffer = await file.arrayBuffer()
    const text = decodeTextFileFromArrayBuffer(arrayBuffer)
    const metadata = parseCrossMetadata(text)
    const parsedEntries = parseCrossResults(text)

    if (parsedEntries.length === 0) {
      throw new Error('Geen herkenbare uitslagregels gevonden in dit bestand.')
    }

    const existingCross = crosses.value.find((cross) => isSameCross(cross, metadata))
    let crossToUse = existingCross

    if (!crossToUse && crosses.value.length >= 3) {
      throw new Error('Maximum van 3 verschillende crossen bereikt.')
    }

    if (crossToUse) {
      const shouldReplace = window.confirm(
        `Wedstrijd bestaat al (${crossToUse.name}, ${formatDate(crossToUse.date)}). Oude uitslag vervangen met nieuwe upload?`,
      )
      if (!shouldReplace) {
        event.target.value = ''
        return
      }
    }

    if (!crossToUse) {
      const insertResult = await db.value.crosses.insert({
        id: `cross-${Date.now()}-${Math.random().toString(36).slice(2, 8)}`,
        name: metadata.name,
        association: metadata.association,
        date: metadata.date,
        createdAt: new Date().toISOString(),
      })
      crossToUse = insertResult.toJSON()
    }
    else {
      const crossDoc = await db.value.crosses.findOne({ selector: { id: crossToUse.id } }).exec()
      if (crossDoc) {
        await crossDoc.patch({
          name: metadata.name,
          association: metadata.association,
          date: metadata.date,
        })
      }
    }

    await clearCrossResults(crossToUse, false)

    await db.value.results.bulkInsert(parsedEntries.map((entry, entryIndex) => ({
      id: `${crossToUse.id}::${sanitizeForId(entry.category)}::${entry.participantKey}::${entry.rank}::${entryIndex}`,
      crossId: crossToUse.id,
      category: entry.category,
      rank: entry.rank,
      points: entry.points,
      participantName: entry.name,
      participantKey: entry.participantKey,
      association: entry.association,
      time: entry.time,
    })))

    await refreshData()
    showSuccess(`${parsedEntries.length} resultaten verwerkt voor ${metadata.name} (${formatDate(metadata.date)}).`)
  }
  catch (error) {
    showError(`Upload mislukt: ${error instanceof Error ? error.message : String(error)}`)
  }
  finally {
    event.target.value = ''
  }
}

const resultCountPerCross = computed(() => {
  const counts = new Map()
  for (const result of results.value) {
    const current = counts.get(result.crossId) || 0
    counts.set(result.crossId, current + 1)
  }
  return counts
})

const canHighlightFinalists = computed(() => {
  if (crosses.value.length < 3) {
    return false
  }

  let uploadedResultsCount = 0
  for (const cross of crosses.value) {
    if ((resultCountPerCross.value.get(cross.id) || 0) > 0) {
      uploadedResultsCount += 1
    }
  }

  return uploadedResultsCount >= 3
})

const decisionsById = computed(() => {
  return new Map(participantDecisions.value.map((decision) => [decision.id, decision]))
})

const crossAssociationDecisionsById = computed(() => {
  return new Map(crossAssociationDecisions.value.map((decision) => [decision.id, decision]))
})

const mergeResolution = computed(() => {
  const groups = new Map()

  for (const result of results.value) {
    const associationKey = canonicalAssociationKey(result.association)
    const groupKey = createGroupKey(result.category, associationKey)
    if (!groups.has(groupKey)) {
      groups.set(groupKey, {
        category: result.category,
        associationKey,
        namesByKey: new Map(),
        parent: new Map(),
        sameDecisions: [],
      })
    }

    const group = groups.get(groupKey)
    if (!group.namesByKey.has(result.participantKey)) {
      group.namesByKey.set(result.participantKey, new Map())
    }
    const nameCounter = group.namesByKey.get(result.participantKey)
    const current = nameCounter.get(result.participantName) || 0
    nameCounter.set(result.participantName, current + 1)
    group.parent.set(result.participantKey, result.participantKey)
  }

  function find(parent, key) {
    const p = parent.get(key)
    if (!p || p === key) {
      return key
    }
    const root = find(parent, p)
    parent.set(key, root)
    return root
  }

  function union(parent, left, right) {
    const rootLeft = find(parent, left)
    const rootRight = find(parent, right)
    if (rootLeft !== rootRight) {
      parent.set(rootRight, rootLeft)
    }
  }

  for (const decision of participantDecisions.value) {
    if (decision.decision !== 'same') {
      continue
    }

    const groupKey = createGroupKey(decision.category, decision.associationKey)
    const group = groups.get(groupKey)
    if (!group) {
      continue
    }
    if (!group.parent.has(decision.leftKey) || !group.parent.has(decision.rightKey)) {
      continue
    }

    union(group.parent, decision.leftKey, decision.rightKey)
    group.sameDecisions.push(decision)
  }

  const canonicalByGroup = new Map()

  for (const [groupKey, group] of groups) {
    const components = new Map()
    for (const key of group.parent.keys()) {
      const root = find(group.parent, key)
      if (!components.has(root)) {
        components.set(root, [])
      }
      components.get(root).push(key)
    }

    const mapForGroup = new Map()
    for (const componentKeys of components.values()) {
      componentKeys.sort()
      let canonicalKey = componentKeys[0]
      let canonicalName
      let newestMatch = null

      for (const decision of group.sameDecisions) {
        if (
          componentKeys.includes(decision.leftKey)
          && componentKeys.includes(decision.rightKey)
        ) {
          if (!newestMatch || decision.updatedAt > newestMatch.updatedAt) {
            newestMatch = decision
          }
        }
      }

      if (newestMatch?.canonicalKey && componentKeys.includes(newestMatch.canonicalKey)) {
        canonicalKey = newestMatch.canonicalKey
      }

      if (newestMatch?.canonicalName) {
        canonicalName = newestMatch.canonicalName
      }
      else {
        const names = group.namesByKey.get(canonicalKey) || new Map()
        const best = Array.from(names.entries()).sort((left, right) => right[1] - left[1])[0]
        canonicalName = best ? best[0] : canonicalKey
      }

      for (const key of componentKeys) {
        mapForGroup.set(key, {
          canonicalKey,
          canonicalName,
        })
      }
    }

    canonicalByGroup.set(groupKey, mapForGroup)
  }

  return canonicalByGroup
})

const associationMergedResults = computed(() => {
  return results.value.map((result) => {
    const associationKey = canonicalAssociationKey(result.association)
    const groupKey = createGroupKey(result.category, associationKey)
    const groupMapping = mergeResolution.value.get(groupKey)
    const mapping = groupMapping?.get(result.participantKey)

    return {
      ...result,
      participantKey: mapping?.canonicalKey || result.participantKey,
      participantName: mapping?.canonicalName || result.participantName,
      associationKey,
    }
  })
})

const crossAssociationMergeResolution = computed(() => {
  const groups = new Map()

  for (const result of associationMergedResults.value) {
    const category = result.category
    if (!groups.has(category)) {
      groups.set(category, {
        parent: new Map(),
        nodes: new Map(),
        sameDecisions: [],
      })
    }
    const group = groups.get(category)
    const nodeKey = `${result.participantKey}::${result.associationKey}`
    if (!group.nodes.has(nodeKey)) {
      group.nodes.set(nodeKey, {
        participantKey: result.participantKey,
        participantName: result.participantName,
        associationKey: result.associationKey,
        association: result.association,
      })
    }
    group.parent.set(nodeKey, nodeKey)
  }

  function find(parent, key) {
    const p = parent.get(key)
    if (!p || p === key) {
      return key
    }
    const root = find(parent, p)
    parent.set(key, root)
    return root
  }

  function union(parent, left, right) {
    const rootLeft = find(parent, left)
    const rootRight = find(parent, right)
    if (rootLeft !== rootRight) {
      parent.set(rootRight, rootLeft)
    }
  }

  for (const decision of crossAssociationDecisions.value) {
    if (decision.decision !== 'same') {
      continue
    }
    const group = groups.get(decision.category)
    if (!group) {
      continue
    }
    const leftNode = `${decision.leftParticipantKey}::${decision.leftAssociationKey}`
    const rightNode = `${decision.rightParticipantKey}::${decision.rightAssociationKey}`
    if (!group.parent.has(leftNode) || !group.parent.has(rightNode)) {
      continue
    }
    union(group.parent, leftNode, rightNode)
    group.sameDecisions.push(decision)
  }

  const resolvedByCategory = new Map()
  for (const [category, group] of groups) {
    const components = new Map()
    for (const key of group.parent.keys()) {
      const root = find(group.parent, key)
      if (!components.has(root)) {
        components.set(root, [])
      }
      components.get(root).push(key)
    }

    const categoryMap = new Map()
    for (const componentKeys of components.values()) {
      componentKeys.sort()
      let canonicalNode = componentKeys[0]
      let canonicalName = group.nodes.get(canonicalNode)?.participantName || ''
      let canonicalAssociationName = group.nodes.get(canonicalNode)?.association || ''
      let newestMatch = null

      for (const decision of group.sameDecisions) {
        const leftNode = `${decision.leftParticipantKey}::${decision.leftAssociationKey}`
        const rightNode = `${decision.rightParticipantKey}::${decision.rightAssociationKey}`
        if (componentKeys.includes(leftNode) && componentKeys.includes(rightNode)) {
          if (!newestMatch || decision.updatedAt > newestMatch.updatedAt) {
            newestMatch = decision
          }
        }
      }

      if (newestMatch) {
        const desiredNode = `${newestMatch.canonicalParticipantKey || ''}::${newestMatch.canonicalAssociationKey || ''}`
        if (newestMatch.canonicalParticipantKey && newestMatch.canonicalAssociationKey && componentKeys.includes(desiredNode)) {
          canonicalNode = desiredNode
        }
        if (newestMatch.canonicalParticipantName) {
          canonicalName = newestMatch.canonicalParticipantName
        }
        if (newestMatch.canonicalAssociationName) {
          canonicalAssociationName = newestMatch.canonicalAssociationName
        }
      }

      const nodeData = group.nodes.get(canonicalNode)
      for (const key of componentKeys) {
        categoryMap.set(key, {
          canonicalParticipantKey: nodeData?.participantKey || key.split('::')[0],
          canonicalParticipantName: canonicalName || nodeData?.participantName || '',
          canonicalAssociationKey: nodeData?.associationKey || key.split('::')[1],
          canonicalAssociationName: canonicalAssociationName || nodeData?.association || '',
        })
      }
    }

    resolvedByCategory.set(category, categoryMap)
  }

  return resolvedByCategory
})

const normalizedResults = computed(() => {
  return associationMergedResults.value.map((result) => {
    const nodeKey = `${result.participantKey}::${result.associationKey}`
    const categoryMap = crossAssociationMergeResolution.value.get(result.category)
    const mapping = categoryMap?.get(nodeKey)

    return {
      ...result,
      participantKey: mapping?.canonicalParticipantKey || result.participantKey,
      participantName: mapping?.canonicalParticipantName || result.participantName,
      associationKey: mapping?.canonicalAssociationKey || result.associationKey,
      association: mapping?.canonicalAssociationName || result.association,
    }
  })
})

const pendingNameConflicts = computed(() => {
  const participantsByGroup = new Map()
  const pending = []

  for (const result of normalizedResults.value) {
    const associationKey = canonicalAssociationKey(result.association)
    const groupKey = createGroupKey(result.category, associationKey)
    if (!participantsByGroup.has(groupKey)) {
      participantsByGroup.set(groupKey, {
        category: result.category,
        associationKey,
        association: result.association,
        participants: new Map(),
      })
    }

    const group = participantsByGroup.get(groupKey)
    if (!group.participants.has(result.participantKey)) {
      group.participants.set(result.participantKey, {
        key: result.participantKey,
        labelName: result.participantName,
        count: 0,
      })
    }
    const participant = group.participants.get(result.participantKey)
    participant.count += 1
  }

  for (const group of participantsByGroup.values()) {
    const participants = Array.from(group.participants.values())
      .filter((item) => item.key.length >= 4)
      .sort((left, right) => left.labelName.localeCompare(right.labelName, 'nl'))

    for (let leftIndex = 0; leftIndex < participants.length; leftIndex += 1) {
      for (let rightIndex = leftIndex + 1; rightIndex < participants.length; rightIndex += 1) {
        const left = participants[leftIndex]
        const right = participants[rightIndex]
        const score = similarity(left.key, right.key)
        if (score < 0.75) {
          continue
        }

        const decisionId = createDecisionId(group.category, group.associationKey, left.key, right.key)
        if (decisionsById.value.has(decisionId)) {
          continue
        }

        const groupMapping = mergeResolution.value.get(createGroupKey(group.category, group.associationKey))
        const mappedLeft = groupMapping?.get(left.key)?.canonicalKey || left.key
        const mappedRight = groupMapping?.get(right.key)?.canonicalKey || right.key
        if (mappedLeft === mappedRight) {
          continue
        }

        pending.push({
          id: decisionId,
          category: group.category,
          association: group.association || '-',
          associationKey: group.associationKey,
          leftKey: left.key,
          leftName: left.labelName,
          rightKey: right.key,
          rightName: right.labelName,
          similarity: score,
        })
      }
    }
  }

  return pending.sort((left, right) => right.similarity - left.similarity)
})

const pendingCrossAssociationChecks = computed(() => {
  const byCategory = new Map()
  const checks = []

  for (const result of associationMergedResults.value) {
    const category = result.category
    if (!byCategory.has(category)) {
      byCategory.set(category, new Map())
    }

    const categoryMap = byCategory.get(category)
    const rowKey = `${result.participantKey}::${result.associationKey}`
    if (!categoryMap.has(rowKey)) {
      categoryMap.set(rowKey, {
        participantKey: result.participantKey,
        participantName: result.participantName,
        associationKey: result.associationKey,
        association: result.association || '-',
      })
    }
  }

  for (const [category, participantsMap] of byCategory) {
    const participants = Array.from(participantsMap.values())
      .filter((participant) => participant.participantKey.length >= 4)
      .sort((left, right) => left.participantName.localeCompare(right.participantName, 'nl'))

    for (let leftIndex = 0; leftIndex < participants.length; leftIndex += 1) {
      for (let rightIndex = leftIndex + 1; rightIndex < participants.length; rightIndex += 1) {
        const left = participants[leftIndex]
        const right = participants[rightIndex]

        if (left.associationKey === right.associationKey) {
          continue
        }

        const score = similarity(left.participantKey, right.participantKey)
        const exactMatch = left.participantKey === right.participantKey
        if (!exactMatch && score < 0.88) {
          continue
        }

        const id = createCrossAssociationDecisionId(
          category,
          left.participantKey,
          left.associationKey,
          right.participantKey,
          right.associationKey,
        )
        if (crossAssociationDecisionsById.value.has(id)) {
          continue
        }

        const categoryMapping = crossAssociationMergeResolution.value.get(category)
        const leftNode = `${left.participantKey}::${left.associationKey}`
        const rightNode = `${right.participantKey}::${right.associationKey}`
        const leftCanonical = categoryMapping?.get(leftNode)
        const rightCanonical = categoryMapping?.get(rightNode)
        if (
          leftCanonical
          && rightCanonical
          && leftCanonical.canonicalParticipantKey === rightCanonical.canonicalParticipantKey
          && leftCanonical.canonicalAssociationKey === rightCanonical.canonicalAssociationKey
        ) {
          continue
        }

        checks.push({
          id,
          category,
          leftParticipantKey: left.participantKey,
          leftAssociationKey: left.associationKey,
          leftName: left.participantName,
          leftAssociation: left.association,
          rightParticipantKey: right.participantKey,
          rightAssociationKey: right.associationKey,
          rightName: right.participantName,
          rightAssociation: right.association,
          similarity: score,
        })
      }
    }
  }

  const uniqueChecks = new Map()
  for (const check of checks) {
    if (!uniqueChecks.has(check.id)) {
      uniqueChecks.set(check.id, check)
    }
  }

  return Array.from(uniqueChecks.values()).sort((left, right) => right.similarity - left.similarity)
})

async function chooseSameAcrossAssociations(check, preferredSide) {
  resetMessages()
  const useLeft = preferredSide === 'left'
  await db.value.crossAssociationDecisions.upsert({
    id: check.id,
    category: check.category,
    leftParticipantKey: check.leftParticipantKey,
    leftAssociationKey: check.leftAssociationKey,
    rightParticipantKey: check.rightParticipantKey,
    rightAssociationKey: check.rightAssociationKey,
    decision: 'same',
    canonicalParticipantKey: useLeft ? check.leftParticipantKey : check.rightParticipantKey,
    canonicalParticipantName: useLeft ? check.leftName : check.rightName,
    canonicalAssociationKey: useLeft ? check.leftAssociationKey : check.rightAssociationKey,
    canonicalAssociationName: useLeft ? check.leftAssociation : check.rightAssociation,
    updatedAt: new Date().toISOString(),
  })
  await refreshData()
  showSuccess(`Samengevoegd over verenigingen als dezelfde deelnemer: ${useLeft ? check.leftName : check.rightName}.`)
}

async function chooseDifferentAcrossAssociations(check) {
  resetMessages()
  await db.value.crossAssociationDecisions.upsert({
    id: check.id,
    category: check.category,
    leftParticipantKey: check.leftParticipantKey,
    leftAssociationKey: check.leftAssociationKey,
    rightParticipantKey: check.rightParticipantKey,
    rightAssociationKey: check.rightAssociationKey,
    decision: 'different',
    canonicalParticipantKey: '',
    canonicalParticipantName: '',
    canonicalAssociationKey: '',
    canonicalAssociationName: '',
    updatedAt: new Date().toISOString(),
  })
  await refreshData()
  showSuccess('Gemarkeerd als aparte deelnemers (over verenigingen).')
}

async function chooseSameParticipant(conflict, preferredSide) {
  resetMessages()
  const canonicalKey = preferredSide === 'left' ? conflict.leftKey : conflict.rightKey
  const canonicalName = preferredSide === 'left' ? conflict.leftName : conflict.rightName

  await db.value.participantDecisions.upsert({
    id: conflict.id,
    category: conflict.category,
    associationKey: conflict.associationKey,
    leftKey: conflict.leftKey,
    rightKey: conflict.rightKey,
    decision: 'same',
    canonicalKey,
    canonicalName,
    updatedAt: new Date().toISOString(),
  })

  await refreshData()
  showSuccess(`Samengevoegd als dezelfde deelnemer: ${canonicalName}.`)
}

async function chooseDifferentParticipants(conflict) {
  resetMessages()
  await db.value.participantDecisions.upsert({
    id: conflict.id,
    category: conflict.category,
    associationKey: conflict.associationKey,
    leftKey: conflict.leftKey,
    rightKey: conflict.rightKey,
    decision: 'different',
    canonicalKey: '',
    canonicalName: '',
    updatedAt: new Date().toISOString(),
  })

  await refreshData()
  showSuccess('Gemarkeerd als aparte deelnemers.')
}

const standingsPerCategory = computed(() => {
  const byCategory = new Map()
  for (const category of CATEGORY_ORDER) {
    byCategory.set(category, new Map())
  }

  const crossIndex = new Map(crosses.value.map((cross, index) => [cross.id, index]))

  for (const result of normalizedResults.value) {
    if (!byCategory.has(result.category)) {
      continue
    }

    const associationKey = result.associationKey
    const participantKey = result.participantKey
    const participantName = result.participantName

    const categoryMap = byCategory.get(result.category)
    const crossPosition = crossIndex.get(result.crossId)
    if (crossPosition === undefined) {
      continue
    }

    const rowKey = `${participantKey}::${associationKey}`
    if (!categoryMap.has(rowKey)) {
      categoryMap.set(rowKey, {
        participantKey: rowKey,
        participantName,
        association: result.association,
        pointsPerCross: Array(crosses.value.length).fill(null),
        starts: 0,
        baseTotal: 0,
        bonus: 0,
        total: 0,
      })
    }

    const participantRow = categoryMap.get(rowKey)
    participantRow.participantName = participantName || participantRow.participantName
    participantRow.association = participantRow.association || result.association
    participantRow.pointsPerCross[crossPosition] = result.points
  }

  const rowsByCategory = new Map()
  for (const [category, participants] of byCategory) {
    const rows = Array.from(participants.values())
    for (const row of rows) {
      row.starts = row.pointsPerCross.filter((value) => value !== null).length
      row.eligibleForPlacement = row.starts >= 2
      row.place = null
      row.bonus = 0
      row.baseTotal = 0
      row.total = 0
      row.isQualifiedForFinal = false
    }

    const bonusForCategory = rows.length <= 10 ? 3 : 5

    for (const row of rows) {
      const twoLowestPoints = row.pointsPerCross
        .filter((value) => value !== null)
        .sort((left, right) => left - right)
        .slice(0, 2)
      row.baseTotal = twoLowestPoints.reduce((sum, points) => sum + points, 0)
      row.bonus = row.starts >= 3 ? bonusForCategory : 0
      row.total = row.baseTotal - row.bonus
    }

    rows.sort((left, right) => {
      if (left.eligibleForPlacement !== right.eligibleForPlacement) {
        return left.eligibleForPlacement ? -1 : 1
      }
      if (left.total !== right.total) {
        return left.total - right.total
      }
      if (left.starts !== right.starts) {
        return right.starts - left.starts
      }
      return left.participantName.localeCompare(right.participantName, 'nl')
    })

    let lastTotal = null
    let lastPlace = null
    let eligibleIndex = 0
    for (const row of rows) {
      if (!row.eligibleForPlacement) {
        continue
      }
      eligibleIndex += 1
      if (lastTotal !== null && row.total === lastTotal) {
        row.place = lastPlace
      }
      else {
        row.place = eligibleIndex
        lastPlace = eligibleIndex
        lastTotal = row.total
      }
    }

    const rankedClassifiedRows = rows.filter((row) => row.eligibleForPlacement)
    const baseFinalists = baseFinalistsCount(rankedClassifiedRows.length)

    if (baseFinalists > 0 && rankedClassifiedRows.length > 0) {
      const cutoffIndex = Math.min(baseFinalists, rankedClassifiedRows.length) - 1
      const cutoffTotal = rankedClassifiedRows[cutoffIndex].total

      for (const row of rankedClassifiedRows) {
        if (row.total <= cutoffTotal) {
          row.isQualifiedForFinal = true
        }
      }
    }
    rowsByCategory.set(category, rows)
  }

  return rowsByCategory
})

const teamStandingsPerCategory = computed(() => {
  const crossIndex = new Map(crosses.value.map((cross, index) => [cross.id, index]))
  const rowsByCategory = new Map()

  for (const config of TEAM_CATEGORY_CONFIG) {
    const rowsByAssociation = new Map()
    const relevant = normalizedResults.value.filter((result) => config.sourceCategories.includes(result.category))

    const effectiveEntries = relevant.map((result) => ({
      crossId: result.crossId,
      associationKey: result.associationKey,
      association: result.association,
      participantName: result.participantName,
      sourceCategory: result.category,
      time: result.time,
      effectivePoints: result.points,
    }))

    const perCrossAssociation = new Map()
    for (const entry of effectiveEntries) {
      if (!crossIndex.has(entry.crossId)) {
        continue
      }
      const key = `${entry.crossId}::${entry.associationKey}`
      if (!perCrossAssociation.has(key)) {
        perCrossAssociation.set(key, [])
      }
      perCrossAssociation.get(key).push(entry)
    }

    for (const [combinedKey, associationEntries] of perCrossAssociation) {
      const [crossId, associationKey] = combinedKey.split('::')
      const crossPosition = crossIndex.get(crossId)
      if (crossPosition === undefined) {
        continue
      }

      const orderedEntries = associationEntries
        .slice()
        .sort((left, right) => {
          if (left.effectivePoints !== right.effectivePoints) {
            return left.effectivePoints - right.effectivePoints
          }
          return left.participantName.localeCompare(right.participantName, 'nl')
        })

      const hasMinimumThree = orderedEntries.length >= 3
      const score = hasMinimumThree
        ? orderedEntries.slice(0, 3).reduce((sum, entry) => sum + entry.effectivePoints, 0)
        : null

      const associationName = associationEntries.find((row) => row.association)?.association || '-'

      if (!rowsByAssociation.has(associationKey)) {
        rowsByAssociation.set(associationKey, {
          category: config.label,
          associationKey,
          association: associationName,
          pointsPerCross: Array(crosses.value.length).fill(null),
          detailsPerCross: Array(crosses.value.length).fill(null),
          starts: 0,
          total: 0,
          place: null,
          eligibleForPlacement: false,
          isQualifiedForFinal: false,
        })
      }

      const row = rowsByAssociation.get(associationKey)
      row.association = row.association || associationName
      row.pointsPerCross[crossPosition] = score
      row.detailsPerCross[crossPosition] = score === null
        ? null
        : {
            participants: orderedEntries
              .slice(0, 3)
              .map((entry) => ({
                name: entry.participantName || '-',
                sourceCategory: entry.sourceCategory || '',
                points: entry.effectivePoints,
                time: entry.time || '-',
              })),
          }
    }

    const rows = Array.from(rowsByAssociation.values())
    for (const row of rows) {
      row.starts = row.pointsPerCross.filter((value) => value !== null).length
      const sortedScores = row.pointsPerCross
        .filter((value) => value !== null)
        .sort((left, right) => left - right)
      row.total = sortedScores.slice(0, 2).reduce((sum, value) => sum + value, 0)
      row.eligibleForPlacement = row.starts >= 2 && !isExcludedFromTeamQualification(row.associationKey)
      row.place = null
      row.isQualifiedForFinal = false
    }

    const visibleRows = rows.filter((row) => row.total > 0)

    visibleRows.sort((left, right) => {
      if (left.eligibleForPlacement !== right.eligibleForPlacement) {
        return left.eligibleForPlacement ? -1 : 1
      }
      if (left.total !== right.total) {
        return left.total - right.total
      }
      if (left.starts !== right.starts) {
        return right.starts - left.starts
      }
      return left.association.localeCompare(right.association, 'nl')
    })

    let lastTotal = null
    let lastPlace = null
    let eligibleIndex = 0
    for (const row of visibleRows) {
      if (!row.eligibleForPlacement) {
        continue
      }
      eligibleIndex += 1
      if (lastTotal !== null && row.total === lastTotal) {
        row.place = lastPlace
      }
      else {
        row.place = eligibleIndex
        lastPlace = eligibleIndex
        lastTotal = row.total
      }
    }

    const classifiedRows = visibleRows.filter((row) => row.eligibleForPlacement)
    const finalists = teamFinalistsCount(classifiedRows.length)
    if (finalists > 0 && classifiedRows.length > 0) {
      const cutoffIndex = Math.min(finalists, classifiedRows.length) - 1
      const cutoffTotal = classifiedRows[cutoffIndex].total
      for (const row of classifiedRows) {
        if (row.total <= cutoffTotal) {
          row.isQualifiedForFinal = true
        }
      }
    }

    rowsByCategory.set(config.label, visibleRows)
  }

  return rowsByCategory
})

onMounted(() => {
  init()
})
</script>

<template>
  <main class="container py-4 py-md-5">
    <div class="d-flex justify-content-between align-items-start gap-3 mb-4">
      <div class="d-flex flex-column gap-2">
        <h1 class="h3 mb-0">
          Jeugdcross competitie
        </h1>
        <p class="text-secondary mb-0">
          Upload wedstrijduitslagen, controleer conflicten en volg het individuele en ploegenklassement.
        </p>
      </div>
      <button
        class="btn btn-outline-danger btn-sm"
        type="button"
        @click="resetAllData"
      >
        Reset alles
      </button>
    </div>

    <div
      v-if="isLoading"
      class="alert alert-info"
    >
      Gegevens worden geladen...
    </div>

    <template v-else>
      <div
        v-if="errorMessage"
        class="alert alert-danger"
      >
        {{ errorMessage }}
      </div>
      <div
        v-if="successMessage"
        class="alert alert-success"
      >
        {{ successMessage }}
      </div>

      <ul class="nav nav-tabs mb-4">
        <li class="nav-item">
          <button
            class="nav-link"
            :class="{ active: activeTab === 'wedstrijden' }"
            type="button"
            @click="activeTab = 'wedstrijden'"
          >
            Wedstrijden
          </button>
        </li>
        <li class="nav-item">
          <button
            class="nav-link"
            :class="{ active: activeTab === 'klassement' }"
            type="button"
            @click="activeTab = 'klassement'"
          >
            Individueel klassement
          </button>
        </li>
        <li class="nav-item">
          <button
            class="nav-link"
            :class="{ active: activeTab === 'ploegen' }"
            type="button"
            @click="activeTab = 'ploegen'"
          >
            Ploegenklassement
          </button>
        </li>
      </ul>

      <template v-if="activeTab === 'wedstrijden'">
        <section class="card shadow-sm mb-4">
          <div class="card-body">
            <h2 class="h5 mb-3">
              Wedstrijdupload
            </h2>
            <p class="text-secondary mb-3">
              Upload direct een uitslagbestand. De wedstrijd wordt automatisch aangemaakt op basis van de kopregels.
            </p>
            <p class="text-secondary small mb-3">
              Maximaal 3 verschillende crossen. Bestaat een wedstrijd al, dan wordt de oude uitslag vervangen.
            </p>
            <label class="btn btn-primary mb-0">
              Upload uitslag
              <input
                class="d-none"
                type="file"
                accept=".txt,text/plain"
                @change="onCompetitionFileSelected"
              >
            </label>
          </div>
        </section>

        <section class="card shadow-sm mb-4">
          <div class="card-body">
            <h2 class="h5 mb-3">
              Crossbeheer & Uploads
            </h2>

            <div
              v-if="crosses.length === 0"
              class="text-secondary"
            >
              Nog geen crossen aangemaakt.
            </div>

            <div
              v-else
              class="table-responsive"
            >
              <table class="table align-middle">
                <thead>
                  <tr>
                    <th>Naam</th>
                    <th>Vereniging</th>
                    <th>Datum</th>
                    <th>Resultaten</th>
                    <th>Acties</th>
                  </tr>
                </thead>
                <tbody>
                  <tr
                    v-for="cross in crosses"
                    :key="cross.id"
                  >
                    <td>{{ cross.name }}</td>
                    <td>{{ cross.association }}</td>
                    <td>{{ formatDate(cross.date) }}</td>
                    <td>{{ resultCountPerCross.get(cross.id) || 0 }}</td>
                    <td>
                      <div class="d-flex flex-wrap gap-2">
                        <button
                          class="btn btn-sm btn-outline-danger"
                          type="button"
                          @click="clearCrossResults(cross)"
                        >
                          Verwijder uitslag
                        </button>
                        <button
                          class="btn btn-sm btn-danger"
                          type="button"
                          @click="removeCross(cross)"
                        >
                          Verwijder wedstrijd
                        </button>
                      </div>
                    </td>
                  </tr>
                </tbody>
              </table>
            </div>
          </div>
        </section>
      </template>

      <template v-else-if="activeTab === 'klassement'">
        <section class="card shadow-sm mb-4">
          <div class="card-body">
            <p class="mb-0">
              Groen gemarkeerd = geplaatst voor de finale.
            </p>
          </div>
        </section>

        <section
          v-if="pendingNameConflicts.length > 0"
          class="card shadow-sm mb-4"
        >
          <div class="card-body">
            <h2 class="h5 mb-3">
              Naamconflicten
            </h2>
            <p class="text-secondary mb-3">
              Vergelijkbare namen binnen dezelfde categorie en vereniging. Kies of dit dezelfde deelnemer is of niet.
            </p>
            <div class="table-responsive">
              <table class="table table-sm align-middle">
                <thead>
                  <tr>
                    <th>Categorie</th>
                    <th>Vereniging</th>
                    <th>Naam A</th>
                    <th>Naam B</th>
                    <th>Gelijkenis</th>
                    <th>Actie</th>
                  </tr>
                </thead>
                <tbody>
                  <tr
                    v-for="conflict in pendingNameConflicts"
                    :key="conflict.id"
                  >
                    <td>{{ conflict.category }}</td>
                    <td>{{ conflict.association }}</td>
                    <td>{{ conflict.leftName }}</td>
                    <td>{{ conflict.rightName }}</td>
                    <td>{{ Math.round(conflict.similarity * 100) }}%</td>
                    <td>
                      <div class="d-flex flex-wrap gap-2">
                        <button
                          class="btn btn-sm btn-outline-success"
                          type="button"
                          @click="chooseSameParticipant(conflict, 'left')"
                        >
                          Zelfde als {{ conflict.leftName }}
                        </button>
                        <button
                          class="btn btn-sm btn-outline-success"
                          type="button"
                          @click="chooseSameParticipant(conflict, 'right')"
                        >
                          Zelfde als {{ conflict.rightName }}
                        </button>
                        <button
                          class="btn btn-sm btn-outline-secondary"
                          type="button"
                          @click="chooseDifferentParticipants(conflict)"
                        >
                          Aparte deelnemers
                        </button>
                      </div>
                    </td>
                  </tr>
                </tbody>
              </table>
            </div>
          </div>
        </section>

        <section
          v-if="pendingCrossAssociationChecks.length > 0"
          class="card shadow-sm mb-4"
        >
          <div class="card-body">
            <h2 class="h5 mb-3">
              Controle Dubbele Namen Over Verenigingen
            </h2>
            <p class="text-secondary mb-3">
              Mogelijk dezelfde deelnemer met een andere vereniging. Dit blokkeert niets, maar is bedoeld voor controle.
            </p>
            <div class="table-responsive">
              <table class="table table-sm align-middle">
                <thead>
                  <tr>
                    <th>Categorie</th>
                    <th>Naam A</th>
                    <th>Vereniging A</th>
                    <th>Naam B</th>
                    <th>Vereniging B</th>
                    <th>Gelijkenis</th>
                    <th>Actie</th>
                  </tr>
                </thead>
                <tbody>
                  <tr
                    v-for="check in pendingCrossAssociationChecks"
                    :key="check.id"
                  >
                    <td>{{ check.category }}</td>
                    <td>{{ check.leftName }}</td>
                    <td>{{ check.leftAssociation }}</td>
                    <td>{{ check.rightName }}</td>
                    <td>{{ check.rightAssociation }}</td>
                    <td>{{ Math.round(check.similarity * 100) }}%</td>
                    <td>
                      <div class="d-flex flex-wrap gap-2">
                        <button
                          class="btn btn-sm btn-outline-success"
                          type="button"
                          @click="chooseSameAcrossAssociations(check, 'left')"
                        >
                          Zelfde als {{ check.leftName }}
                        </button>
                        <button
                          class="btn btn-sm btn-outline-success"
                          type="button"
                          @click="chooseSameAcrossAssociations(check, 'right')"
                        >
                          Zelfde als {{ check.rightName }}
                        </button>
                        <button
                          class="btn btn-sm btn-outline-secondary"
                          type="button"
                          @click="chooseDifferentAcrossAssociations(check)"
                        >
                          Aparte deelnemers
                        </button>
                      </div>
                    </td>
                  </tr>
                </tbody>
              </table>
            </div>
          </div>
        </section>

        <section class="d-flex flex-column gap-4">
          <article
            v-for="category in CATEGORY_ORDER"
            :key="category"
            class="card shadow-sm"
          >
            <div class="card-body">
              <h3 class="h6 mb-3">
                {{ category }}
              </h3>
              <div class="table-responsive">
                <table class="table table-striped table-sm align-middle">
                  <thead>
                    <tr>
                      <th>Plaats</th>
                      <th>Naam</th>
                      <th>Vereniging</th>
                      <th
                        v-for="cross in crosses"
                        :key="`${category}-${cross.id}`"
                      >
                        {{ formatDate(cross.date) }}
                      </th>
                      <th>Bonus</th>
                      <th>Totaal</th>
                    </tr>
                  </thead>
                  <tbody>
                    <tr
                      v-for="row in standingsPerCategory.get(category) || []"
                      :key="`${category}-${row.participantKey}`"
                      :class="{ 'table-success': row.isQualifiedForFinal && canHighlightFinalists }"
                    >
                      <td class="fw-semibold">
                        {{ row.place ?? '-' }}
                      </td>
                      <td>{{ row.participantName }}</td>
                      <td>{{ row.association || '-' }}</td>
                      <td
                        v-for="(points, index) in row.pointsPerCross"
                        :key="`${row.participantKey}-${index}`"
                      >
                        {{ points ?? '-' }}
                      </td>
                      <td class="fw-semibold">
                        {{ row.bonus }}
                      </td>
                      <td class="fw-semibold">
                        {{ row.total }}
                      </td>
                    </tr>
                    <tr v-if="(standingsPerCategory.get(category) || []).length === 0">
                      <td
                        class="text-secondary"
                        :colspan="5 + crosses.length"
                      >
                        Geen deelnemers voor deze categorie.
                      </td>
                    </tr>
                  </tbody>
                </table>
              </div>
            </div>
          </article>
        </section>
      </template>

      <template v-else>
        <section class="card shadow-sm mb-4">
          <div class="card-body">
            <p class="mb-0">
              Groen gemarkeerde ploegen hebben zich geplaatst voor de finale en mogen eventueel aangevuld worden tot 4 lopers,
              indien die aan minstens 2 wedstrijden meegedaan hebben (zie competitiereglement art 16.b).
            </p>
          </div>
        </section>

        <section class="d-flex flex-column gap-4">
          <article
            v-for="config in TEAM_CATEGORY_CONFIG"
            :key="config.label"
            class="card shadow-sm"
          >
            <div class="card-body">
              <h3 class="h6 mb-3">
                {{ config.label }}
              </h3>
              <div class="table-responsive">
                <table class="table table-striped table-sm align-middle">
                  <thead>
                    <tr>
                      <th>Plaats</th>
                      <th>Vereniging</th>
                      <th
                        v-for="cross in crosses"
                        :key="`${config.label}-${cross.id}`"
                      >
                        {{ formatDate(cross.date) }}
                      </th>
                      <th>Totaal</th>
                    </tr>
                  </thead>
                  <tbody>
                    <tr
                      v-for="row in teamStandingsPerCategory.get(config.label) || []"
                      :key="`${config.label}-${row.associationKey}`"
                      :class="{ 'table-success': row.isQualifiedForFinal && canHighlightFinalists }"
                    >
                      <td class="fw-semibold">
                        {{ row.place ?? '-' }}
                      </td>
                      <td>{{ row.association || '-' }}</td>
                      <td
                        v-for="(points, index) in row.pointsPerCross"
                        :key="`${row.associationKey}-${index}`"
                      >
                        <button
                          v-if="points !== null"
                          class="btn btn-link btn-sm p-0 text-decoration-none"
                          type="button"
                          @click="openTeamBreakdown(row, config.label, config.sourceCategories.length > 1, crosses[index], index)"
                        >
                          {{ points }}
                        </button>
                        <span v-else>-</span>
                      </td>
                      <td class="fw-semibold">
                        {{ row.total }}
                      </td>
                    </tr>
                    <tr v-if="(teamStandingsPerCategory.get(config.label) || []).length === 0">
                      <td
                        class="text-secondary"
                        :colspan="3 + crosses.length"
                      >
                        Geen ploegen voor deze categorie.
                      </td>
                    </tr>
                  </tbody>
                </table>
              </div>
            </div>
          </article>
        </section>
      </template>
    </template>

    <div
      v-if="teamBreakdownModal"
      class="modal d-block"
      tabindex="-1"
      style="background-color: rgba(0, 0, 0, 0.45);"
      @click.self="closeTeamBreakdown"
    >
      <div class="modal-dialog modal-lg">
        <div class="modal-content">
          <div class="modal-header">
            <h5 class="modal-title">
              Ploegpunten Uitleg
            </h5>
            <button
              type="button"
              class="btn-close"
              aria-label="Close"
              @click="closeTeamBreakdown"
            />
          </div>
          <div class="modal-body">
            <p class="mb-1">
              <strong>Categorie:</strong> {{ teamBreakdownModal.categoryLabel }}
            </p>
            <p class="mb-1">
              <strong>Vereniging:</strong> {{ teamBreakdownModal.association }}
            </p>
            <p class="mb-1">
              <strong>Wedstrijd:</strong> {{ teamBreakdownModal.crossName }} ({{ teamBreakdownModal.crossDate }})
            </p>
            <p class="mb-3">
              <strong>Ploegscore:</strong> {{ teamBreakdownModal.score }}
            </p>

            <div class="table-responsive">
              <table class="table table-sm align-middle">
                <thead>
                  <tr>
                    <th>Deelnemer</th>
                    <th>Punten</th>
                    <th>Tijd</th>
                  </tr>
                </thead>
                <tbody>
                  <tr
                    v-for="participant in teamBreakdownModal.participants"
                    :key="`${participant.name}-${participant.time}-${participant.points}`"
                  >
                    <td>{{ participantDisplayName(participant, teamBreakdownModal.isCombinedCategory) }}</td>
                    <td>{{ participant.points }}</td>
                    <td>{{ participant.time }}</td>
                  </tr>
                </tbody>
              </table>
            </div>
          </div>
          <div class="modal-footer">
            <button
              type="button"
              class="btn btn-secondary"
              @click="closeTeamBreakdown"
            >
              Sluiten
            </button>
          </div>
        </div>
      </div>
    </div>
  </main>
</template>

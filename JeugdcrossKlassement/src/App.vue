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
let notificationTimeoutId = null

const crosses = ref([])
const results = ref([])
const participantDecisions = ref([])

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

async function refreshData() {
  if (!db.value) {
    return
  }

  const crossDocs = await db.value.crosses.find().sort({ date: 'asc' }).exec()
  const resultDocs = await db.value.results.find().exec()
  const decisionDocs = await db.value.participantDecisions.find().exec()

  crosses.value = crossDocs.map((doc) => doc.toJSON())
  results.value = resultDocs.map((doc) => doc.toJSON())
  participantDecisions.value = decisionDocs.map((doc) => doc.toJSON())
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

const pendingNameConflicts = computed(() => {
  const participantsByGroup = new Map()
  const pending = []

  for (const result of results.value) {
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

  for (const result of results.value) {
    if (!byCategory.has(result.category)) {
      continue
    }

    const associationKey = canonicalAssociationKey(result.association)
    const groupKey = createGroupKey(result.category, associationKey)
    const groupMapping = mergeResolution.value.get(groupKey)
    const mapping = groupMapping?.get(result.participantKey)

    const participantKey = mapping?.canonicalKey || result.participantKey
    const participantName = mapping?.canonicalName || result.participantName

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
        rawTotal: 0,
        bonus: 0,
        total: 0,
      })
    }

    const participantRow = categoryMap.get(rowKey)
    participantRow.participantName = participantName || participantRow.participantName
    participantRow.association = participantRow.association || result.association
    participantRow.pointsPerCross[crossPosition] = result.points
    participantRow.rawTotal += result.points
  }

  const rowsByCategory = new Map()
  for (const [category, participants] of byCategory) {
    const rows = Array.from(participants.values())
    for (const row of rows) {
      row.starts = row.pointsPerCross.filter((value) => value !== null).length
      row.eligibleForPlacement = row.starts >= 2
      row.place = null
      row.bonus = 0
      row.total = row.rawTotal
      row.isQualifiedForFinal = false
    }

    const classifiedRows = rows.filter((row) => row.eligibleForPlacement)
    const bonusForCategory = rows.length <= 10 ? 3 : 5

    for (const row of rows) {
      row.bonus = row.starts >= 3 ? bonusForCategory : 0
      row.total = row.rawTotal - row.bonus
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

    let placeCounter = 1
    for (const row of rows) {
      if (!row.eligibleForPlacement) {
        continue
      }
      row.place = placeCounter
      placeCounter += 1
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

onMounted(() => {
  init()
})
</script>

<template>
  <main class="container py-4 py-md-5">
    <div class="d-flex justify-content-between align-items-start gap-3 mb-4">
      <div class="d-flex flex-column gap-2">
        <h1 class="h3 mb-0">
          Jeugdcross Klassement
        </h1>
        <p class="text-secondary mb-0">
          Individueel klassement op basis van punten per cross (1e plaats = 1 punt).
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

      <template v-else>
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
              <p class="small text-secondary mb-3">
                <template v-if="canHighlightFinalists">
                  Groen gemarkeerd = geplaatst voor de finale.
                </template>
                <template v-else>
                  Finale-markering verschijnt zodra de 3 wedstrijd-uitslagen zijn geupload.
                </template>
              </p>
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
    </template>
  </main>
</template>

import { addRxPlugin, createRxDatabase } from 'rxdb'
import { RxDBQueryBuilderPlugin } from 'rxdb/plugins/query-builder'
import { getRxStorageDexie } from 'rxdb/plugins/storage-dexie'

addRxPlugin(RxDBQueryBuilderPlugin)

const crossSchema = {
  title: 'cross schema',
  version: 0,
  primaryKey: 'id',
  type: 'object',
  properties: {
    id: {
      type: 'string',
      maxLength: 120,
    },
    name: {
      type: 'string',
    },
    association: {
      type: 'string',
    },
    date: {
      type: 'string',
      maxLength: 10,
    },
    createdAt: {
      type: 'string',
    },
  },
  required: ['id', 'name', 'association', 'date', 'createdAt'],
  indexes: ['date'],
}

const resultSchema = {
  title: 'result schema',
  version: 0,
  primaryKey: 'id',
  type: 'object',
  properties: {
    id: {
      type: 'string',
      maxLength: 220,
    },
    crossId: {
      type: 'string',
      maxLength: 120,
    },
    category: {
      type: 'string',
    },
    rank: {
      type: 'number',
      minimum: 1,
      maximum: 9999,
      multipleOf: 1,
    },
    points: {
      type: 'number',
      minimum: 1,
      maximum: 9999,
      multipleOf: 1,
    },
    participantName: {
      type: 'string',
    },
    participantKey: {
      type: 'string',
      maxLength: 220,
    },
    association: {
      type: 'string',
    },
    time: {
      type: 'string',
    },
  },
  required: [
    'id',
    'crossId',
    'category',
    'rank',
    'points',
    'participantName',
    'participantKey',
    'association',
    'time',
  ],
  indexes: ['crossId', ['category', 'participantKey']],
}

const participantDecisionSchema = {
  title: 'participant decision schema',
  version: 0,
  primaryKey: 'id',
  type: 'object',
  properties: {
    id: {
      type: 'string',
      maxLength: 320,
    },
    category: {
      type: 'string',
    },
    associationKey: {
      type: 'string',
      maxLength: 220,
    },
    leftKey: {
      type: 'string',
      maxLength: 220,
    },
    rightKey: {
      type: 'string',
      maxLength: 220,
    },
    decision: {
      type: 'string',
      enum: ['same', 'different'],
    },
    canonicalKey: {
      type: 'string',
      maxLength: 220,
    },
    canonicalName: {
      type: 'string',
    },
    updatedAt: {
      type: 'string',
    },
  },
  required: ['id', 'category', 'associationKey', 'leftKey', 'rightKey', 'decision', 'updatedAt'],
  indexes: [['category', 'associationKey']],
}

const crossAssociationDecisionSchema = {
  title: 'cross association decision schema',
  version: 0,
  primaryKey: 'id',
  type: 'object',
  properties: {
    id: {
      type: 'string',
      maxLength: 420,
    },
    category: {
      type: 'string',
    },
    leftParticipantKey: {
      type: 'string',
      maxLength: 220,
    },
    leftAssociationKey: {
      type: 'string',
      maxLength: 220,
    },
    rightParticipantKey: {
      type: 'string',
      maxLength: 220,
    },
    rightAssociationKey: {
      type: 'string',
      maxLength: 220,
    },
    decision: {
      type: 'string',
      enum: ['same', 'different'],
    },
    canonicalParticipantKey: {
      type: 'string',
      maxLength: 220,
    },
    canonicalParticipantName: {
      type: 'string',
    },
    canonicalAssociationKey: {
      type: 'string',
      maxLength: 220,
    },
    canonicalAssociationName: {
      type: 'string',
    },
    updatedAt: {
      type: 'string',
    },
  },
  required: [
    'id',
    'category',
    'leftParticipantKey',
    'leftAssociationKey',
    'rightParticipantKey',
    'rightAssociationKey',
    'decision',
    'updatedAt',
  ],
  indexes: [['category', 'leftParticipantKey'], ['category', 'rightParticipantKey']],
}

let dbPromise = null

export function createCrossId() {
  return `cross-${Date.now()}-${Math.random().toString(36).slice(2, 8)}`
}

export async function getDatabase() {
  if (!dbPromise) {
    dbPromise = createRxDatabase({
      name: 'jeugdcrossklassement',
      storage: getRxStorageDexie(),
      multiInstance: false,
    }).then(async (db) => {
      await db.addCollections({
        crosses: {
          schema: crossSchema,
        },
        results: {
          schema: resultSchema,
        },
        participantDecisions: {
          schema: participantDecisionSchema,
        },
        crossAssociationDecisions: {
          schema: crossAssociationDecisionSchema,
        },
      })
      return db
    })
  }

  return dbPromise
}

export async function resetDatabase() {
  if (!dbPromise) {
    return
  }

  const db = await dbPromise
  await db.remove()
  dbPromise = null
}

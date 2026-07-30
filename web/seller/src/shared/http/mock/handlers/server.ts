/* eslint-disable @typescript-eslint/no-explicit-any */
import type MockAdapter from 'axios-mock-adapter'
import { loadSeedData, saveSeedData, advanceServerHistory } from '../data/seed'

export function registerServerMonitorHandlers(mock: MockAdapter): void {
  mock.onGet('/admin/server-monitor/snapshot').reply(() => {
    const seed = loadSeedData()
    advanceServerHistory(seed)
    saveSeedData(seed)
    return [200, { code: 200, message: 'OK', data: seed.serverSnapshot }]
  })

  mock.onGet('/admin/server-monitor/history').reply((config) => {
    const seed = loadSeedData()
    const metric = config.params?.metric || 'cpu'
    const history = seed.serverHistory as any
    const points = history[metric === 'disk-io' ? 'diskIo' : metric] || []
    return [200, { code: 200, message: 'OK', data: { metric, points } }]
  })
}

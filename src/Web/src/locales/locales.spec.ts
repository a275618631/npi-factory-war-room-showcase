import { describe, it, expect } from 'vitest'
import zhTW from './zh-TW.json'
import enUS from './en-US.json'

describe('Localization Resources (Frontend i18n)', () => {
  const coreTerms = [
    'Project',
    'Stage',
    'Issue',
    'Blocker',
    'Part',
    'Station',
    'Yield',
  ]

  it('both zh-TW and en-US contain all 7 core terms', () => {
    for (const term of coreTerms) {
      expect(zhTW).toHaveProperty(term)
      expect(enUS).toHaveProperty(term)

      expect((zhTW as Record<string, string>)[term]).toBeTruthy()
      expect((enUS as Record<string, string>)[term]).toBeTruthy()
    }

    // 驗證核心名詞繁中翻譯
    expect(zhTW.Project).toBe('專案')
    expect(zhTW.Stage).toBe('關卡')
    expect(zhTW.Issue).toBe('問題')
    expect(zhTW.Blocker).toBe('卡關')
    expect(zhTW.Part).toBe('料件')
    expect(zhTW.Station).toBe('工站')
    expect(zhTW.Yield).toBe('良率')

    // 驗證核心名詞英文翻譯
    expect(enUS.Project).toBe('Project')
    expect(enUS.Stage).toBe('Stage')
    expect(enUS.Issue).toBe('Issue')
    expect(enUS.Blocker).toBe('Blocker')
    expect(enUS.Part).toBe('Part')
    expect(enUS.Station).toBe('Station')
    expect(enUS.Yield).toBe('Yield')
  })

  it('zh-TW and en-US have the exact same set of keys without missing any key', () => {
    const zhKeys = Object.keys(zhTW).sort()
    const enKeys = Object.keys(enUS).sort()

    expect(zhKeys.length).toBeGreaterThan(0)
    expect(zhKeys.length).toBe(enKeys.length)
    expect(zhKeys).toEqual(enKeys)
  })
})



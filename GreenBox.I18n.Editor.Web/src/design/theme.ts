import { theme, type ThemeConfig } from 'antd'
import { layoutTokens } from './layoutTokens'

export const editorTheme: ThemeConfig = {
  algorithm: [theme.darkAlgorithm, theme.compactAlgorithm],
  token: {
    borderRadius: layoutTokens.radius.control,
    borderRadiusLG: layoutTokens.radius.surface,
    controlHeight: layoutTokens.controlHeight,
  },
}

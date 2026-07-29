import type { ThemeConfig } from 'ant-design-vue/es/config-provider'

/**
 * Ant Design Vue 4.x 主题配置
 *
 * 与 design-tokens.css 中的 CSS 变量保持一致，由 app/provider.vue 注入。
 */
export const antdTheme: ThemeConfig = {
  token: {
    colorPrimary: '#1677FF',
    colorSuccess: '#52C41A',
    colorWarning: '#FAAD14',
    colorError: '#FF4D4F',
    colorInfo: '#1677FF',
    borderRadius: 6,
    fontFamily:
      '"PingFang SC","Microsoft YaHei",-apple-system,BlinkMacSystemFont,"Segoe UI",sans-serif',
    fontSize: 14,
  },
  components: {
    Table: {
      rowHoverBg: '#FAFAFA',
      headerBg: '#FAFAFA',
      headerColor: '#595959',
      cellPaddingBlock: 12,
      cellPaddingInline: 16,
    },
    Menu: {
      darkItemBg: '#001529',
      darkItemSelectedBg: '#1677FF',
      darkItemColor: '#ffffffd9',
      darkItemHoverColor: '#ffffff',
    },
    Layout: {
      siderBg: '#001529',
      headerBg: '#ffffff',
      headerHeight: 64,
      headerPadding: '0 24px',
      footerBg: '#ffffff',
      footerPadding: '0 50px',
    },
    Button: {
      borderRadius: 6,
      controlHeight: 32,
    },
  },
}

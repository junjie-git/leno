// vite.config.ts
import { defineConfig } from "file:///D:/work/learn/Leno/node_modules/.pnpm/vite@6.4.3_@types+node@20.19.43/node_modules/vite/dist/node/index.js";
import vue from "file:///D:/work/learn/Leno/node_modules/.pnpm/@vitejs+plugin-vue@5.2.4_vi_af122e3d9e27fec923ab2c100af821c7/node_modules/@vitejs/plugin-vue/dist/index.mjs";
import path from "node:path";
import { fileURLToPath } from "node:url";
var __vite_injected_original_import_meta_url = "file:///D:/work/learn/Leno/web/operations/vite.config.ts";
var __dirname = path.dirname(fileURLToPath(__vite_injected_original_import_meta_url));
var vite_config_default = defineConfig({
  plugins: [vue()],
  resolve: {
    alias: {
      "@": path.resolve(__dirname, "src")
    }
  },
  server: {
    port: 5175,
    proxy: {
      "/api": {
        target: "http://localhost:5001",
        changeOrigin: true
      }
    }
  },
  build: {
    target: "es2022",
    sourcemap: true,
    rollupOptions: {
      output: {
        manualChunks: {
          vue: ["vue", "vue-router", "pinia"],
          antd: ["ant-design-vue", "@ant-design/icons-vue"],
          echarts: ["echarts", "vue-echarts"]
        }
      }
    }
  },
  test: {
    environment: "jsdom",
    globals: true,
    setupFiles: "./tests/setup.ts",
    exclude: [
      "**/node_modules/**",
      "**/dist/**",
      "**/cypress/**",
      "**/.{idea,git,cache,output,temp}/**",
      "**/{karma,rollup,webpack,vite,vitest,jest,ava,babel,nyc,cypress,tsup,build}.config.*",
      "tests/e2e/**"
    ],
    coverage: {
      provider: "v8",
      reporter: ["text", "html", "json-summary"],
      include: ["src/**/*.ts", "src/**/*.vue"],
      exclude: [
        "src/**/*.spec.ts",
        "src/main.ts",
        "src/app/provider.vue",
        // 仅统计测试运行时实际加载的文件（视图页由 e2e 冒烟覆盖，不计入单测覆盖率）
        // all: false 时未被任何测试加载的文件（如各 views 页面、mock 数据种子）不进入报告
        "src/shared/http/mock/data/**",
        "src/**/types/**",
        "src/**/*.dto.ts"
      ],
      all: false,
      thresholds: {
        lines: 70,
        functions: 70,
        branches: 60,
        statements: 70
      }
    }
  }
});
export {
  vite_config_default as default
};
//# sourceMappingURL=data:application/json;base64,ewogICJ2ZXJzaW9uIjogMywKICAic291cmNlcyI6IFsidml0ZS5jb25maWcudHMiXSwKICAic291cmNlc0NvbnRlbnQiOiBbImNvbnN0IF9fdml0ZV9pbmplY3RlZF9vcmlnaW5hbF9kaXJuYW1lID0gXCJEOlxcXFx3b3JrXFxcXGxlYXJuXFxcXExlbm9cXFxcd2ViXFxcXG9wZXJhdGlvbnNcIjtjb25zdCBfX3ZpdGVfaW5qZWN0ZWRfb3JpZ2luYWxfZmlsZW5hbWUgPSBcIkQ6XFxcXHdvcmtcXFxcbGVhcm5cXFxcTGVub1xcXFx3ZWJcXFxcb3BlcmF0aW9uc1xcXFx2aXRlLmNvbmZpZy50c1wiO2NvbnN0IF9fdml0ZV9pbmplY3RlZF9vcmlnaW5hbF9pbXBvcnRfbWV0YV91cmwgPSBcImZpbGU6Ly8vRDovd29yay9sZWFybi9MZW5vL3dlYi9vcGVyYXRpb25zL3ZpdGUuY29uZmlnLnRzXCI7aW1wb3J0IHsgZGVmaW5lQ29uZmlnIH0gZnJvbSAndml0ZSdcclxuaW1wb3J0IHZ1ZSBmcm9tICdAdml0ZWpzL3BsdWdpbi12dWUnXHJcbmltcG9ydCBwYXRoIGZyb20gJ25vZGU6cGF0aCdcclxuaW1wb3J0IHsgZmlsZVVSTFRvUGF0aCB9IGZyb20gJ25vZGU6dXJsJ1xyXG5cclxuY29uc3QgX19kaXJuYW1lID0gcGF0aC5kaXJuYW1lKGZpbGVVUkxUb1BhdGgoaW1wb3J0Lm1ldGEudXJsKSlcclxuXHJcbmV4cG9ydCBkZWZhdWx0IGRlZmluZUNvbmZpZyh7XHJcbiAgcGx1Z2luczogW3Z1ZSgpXSxcclxuICByZXNvbHZlOiB7XHJcbiAgICBhbGlhczoge1xyXG4gICAgICAnQCc6IHBhdGgucmVzb2x2ZShfX2Rpcm5hbWUsICdzcmMnKSxcclxuICAgIH0sXHJcbiAgfSxcclxuICBzZXJ2ZXI6IHtcclxuICAgIHBvcnQ6IDUxNzUsXHJcbiAgICBwcm94eToge1xyXG4gICAgICAnL2FwaSc6IHtcclxuICAgICAgICB0YXJnZXQ6ICdodHRwOi8vbG9jYWxob3N0OjUwMDEnLFxyXG4gICAgICAgIGNoYW5nZU9yaWdpbjogdHJ1ZSxcclxuICAgICAgfSxcclxuICAgIH0sXHJcbiAgfSxcclxuICBidWlsZDoge1xyXG4gICAgdGFyZ2V0OiAnZXMyMDIyJyxcclxuICAgIHNvdXJjZW1hcDogdHJ1ZSxcclxuICAgIHJvbGx1cE9wdGlvbnM6IHtcclxuICAgICAgb3V0cHV0OiB7XHJcbiAgICAgICAgbWFudWFsQ2h1bmtzOiB7XHJcbiAgICAgICAgICB2dWU6IFsndnVlJywgJ3Z1ZS1yb3V0ZXInLCAncGluaWEnXSxcclxuICAgICAgICAgIGFudGQ6IFsnYW50LWRlc2lnbi12dWUnLCAnQGFudC1kZXNpZ24vaWNvbnMtdnVlJ10sXHJcbiAgICAgICAgICBlY2hhcnRzOiBbJ2VjaGFydHMnLCAndnVlLWVjaGFydHMnXSxcclxuICAgICAgICB9LFxyXG4gICAgICB9LFxyXG4gICAgfSxcclxuICB9LFxyXG4gIHRlc3Q6IHtcclxuICAgIGVudmlyb25tZW50OiAnanNkb20nLFxyXG4gICAgZ2xvYmFsczogdHJ1ZSxcclxuICAgIHNldHVwRmlsZXM6ICcuL3Rlc3RzL3NldHVwLnRzJyxcclxuICAgIGV4Y2x1ZGU6IFtcclxuICAgICAgJyoqL25vZGVfbW9kdWxlcy8qKicsXHJcbiAgICAgICcqKi9kaXN0LyoqJyxcclxuICAgICAgJyoqL2N5cHJlc3MvKionLFxyXG4gICAgICAnKiovLntpZGVhLGdpdCxjYWNoZSxvdXRwdXQsdGVtcH0vKionLFxyXG4gICAgICAnKiove2thcm1hLHJvbGx1cCx3ZWJwYWNrLHZpdGUsdml0ZXN0LGplc3QsYXZhLGJhYmVsLG55YyxjeXByZXNzLHRzdXAsYnVpbGR9LmNvbmZpZy4qJyxcclxuICAgICAgJ3Rlc3RzL2UyZS8qKicsXHJcbiAgICBdLFxyXG4gICAgY292ZXJhZ2U6IHtcclxuICAgICAgcHJvdmlkZXI6ICd2OCcsXHJcbiAgICAgIHJlcG9ydGVyOiBbJ3RleHQnLCAnaHRtbCcsICdqc29uLXN1bW1hcnknXSxcclxuICAgICAgaW5jbHVkZTogWydzcmMvKiovKi50cycsICdzcmMvKiovKi52dWUnXSxcclxuICAgICAgZXhjbHVkZTogW1xyXG4gICAgICAgICdzcmMvKiovKi5zcGVjLnRzJyxcclxuICAgICAgICAnc3JjL21haW4udHMnLFxyXG4gICAgICAgICdzcmMvYXBwL3Byb3ZpZGVyLnZ1ZScsXHJcbiAgICAgICAgLy8gXHU0RUM1XHU3RURGXHU4QkExXHU2RDRCXHU4QkQ1XHU4RkQwXHU4ODRDXHU2NUY2XHU1QjlFXHU5NjQ1XHU1MkEwXHU4RjdEXHU3Njg0XHU2NTg3XHU0RUY2XHVGRjA4XHU4OUM2XHU1NkZFXHU5ODc1XHU3NTMxIGUyZSBcdTUxOTJcdTcwREZcdTg5ODZcdTc2RDZcdUZGMENcdTRFMERcdThCQTFcdTUxNjVcdTUzNTVcdTZENEJcdTg5ODZcdTc2RDZcdTczODdcdUZGMDlcclxuICAgICAgICAvLyBhbGw6IGZhbHNlIFx1NjVGNlx1NjcyQVx1ODhBQlx1NEVGQlx1NEY1NVx1NkQ0Qlx1OEJENVx1NTJBMFx1OEY3RFx1NzY4NFx1NjU4N1x1NEVGNlx1RkYwOFx1NTk4Mlx1NTQwNCB2aWV3cyBcdTk4NzVcdTk3NjJcdTMwMDFtb2NrIFx1NjU3MFx1NjM2RVx1NzlDRFx1NUI1MFx1RkYwOVx1NEUwRFx1OEZEQlx1NTE2NVx1NjJBNVx1NTQ0QVxyXG4gICAgICAgICdzcmMvc2hhcmVkL2h0dHAvbW9jay9kYXRhLyoqJyxcclxuICAgICAgICAnc3JjLyoqL3R5cGVzLyoqJyxcclxuICAgICAgICAnc3JjLyoqLyouZHRvLnRzJyxcclxuICAgICAgXSxcclxuICAgICAgYWxsOiBmYWxzZSxcclxuICAgICAgdGhyZXNob2xkczoge1xyXG4gICAgICAgIGxpbmVzOiA3MCxcclxuICAgICAgICBmdW5jdGlvbnM6IDcwLFxyXG4gICAgICAgIGJyYW5jaGVzOiA2MCxcclxuICAgICAgICBzdGF0ZW1lbnRzOiA3MCxcclxuICAgICAgfSxcclxuICAgIH0sXHJcbiAgfSxcclxufSlcclxuIl0sCiAgIm1hcHBpbmdzIjogIjtBQUFpUyxTQUFTLG9CQUFvQjtBQUM5VCxPQUFPLFNBQVM7QUFDaEIsT0FBTyxVQUFVO0FBQ2pCLFNBQVMscUJBQXFCO0FBSHVKLElBQU0sMkNBQTJDO0FBS3RPLElBQU0sWUFBWSxLQUFLLFFBQVEsY0FBYyx3Q0FBZSxDQUFDO0FBRTdELElBQU8sc0JBQVEsYUFBYTtBQUFBLEVBQzFCLFNBQVMsQ0FBQyxJQUFJLENBQUM7QUFBQSxFQUNmLFNBQVM7QUFBQSxJQUNQLE9BQU87QUFBQSxNQUNMLEtBQUssS0FBSyxRQUFRLFdBQVcsS0FBSztBQUFBLElBQ3BDO0FBQUEsRUFDRjtBQUFBLEVBQ0EsUUFBUTtBQUFBLElBQ04sTUFBTTtBQUFBLElBQ04sT0FBTztBQUFBLE1BQ0wsUUFBUTtBQUFBLFFBQ04sUUFBUTtBQUFBLFFBQ1IsY0FBYztBQUFBLE1BQ2hCO0FBQUEsSUFDRjtBQUFBLEVBQ0Y7QUFBQSxFQUNBLE9BQU87QUFBQSxJQUNMLFFBQVE7QUFBQSxJQUNSLFdBQVc7QUFBQSxJQUNYLGVBQWU7QUFBQSxNQUNiLFFBQVE7QUFBQSxRQUNOLGNBQWM7QUFBQSxVQUNaLEtBQUssQ0FBQyxPQUFPLGNBQWMsT0FBTztBQUFBLFVBQ2xDLE1BQU0sQ0FBQyxrQkFBa0IsdUJBQXVCO0FBQUEsVUFDaEQsU0FBUyxDQUFDLFdBQVcsYUFBYTtBQUFBLFFBQ3BDO0FBQUEsTUFDRjtBQUFBLElBQ0Y7QUFBQSxFQUNGO0FBQUEsRUFDQSxNQUFNO0FBQUEsSUFDSixhQUFhO0FBQUEsSUFDYixTQUFTO0FBQUEsSUFDVCxZQUFZO0FBQUEsSUFDWixTQUFTO0FBQUEsTUFDUDtBQUFBLE1BQ0E7QUFBQSxNQUNBO0FBQUEsTUFDQTtBQUFBLE1BQ0E7QUFBQSxNQUNBO0FBQUEsSUFDRjtBQUFBLElBQ0EsVUFBVTtBQUFBLE1BQ1IsVUFBVTtBQUFBLE1BQ1YsVUFBVSxDQUFDLFFBQVEsUUFBUSxjQUFjO0FBQUEsTUFDekMsU0FBUyxDQUFDLGVBQWUsY0FBYztBQUFBLE1BQ3ZDLFNBQVM7QUFBQSxRQUNQO0FBQUEsUUFDQTtBQUFBLFFBQ0E7QUFBQTtBQUFBO0FBQUEsUUFHQTtBQUFBLFFBQ0E7QUFBQSxRQUNBO0FBQUEsTUFDRjtBQUFBLE1BQ0EsS0FBSztBQUFBLE1BQ0wsWUFBWTtBQUFBLFFBQ1YsT0FBTztBQUFBLFFBQ1AsV0FBVztBQUFBLFFBQ1gsVUFBVTtBQUFBLFFBQ1YsWUFBWTtBQUFBLE1BQ2Q7QUFBQSxJQUNGO0FBQUEsRUFDRjtBQUNGLENBQUM7IiwKICAibmFtZXMiOiBbXQp9Cg==

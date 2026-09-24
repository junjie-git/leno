// vite.config.ts
import { defineConfig } from "file:///D:/work/learn/Leno/node_modules/.pnpm/vite@6.4.3_@types+node@20.19.43/node_modules/vite/dist/node/index.js";
import vue from "file:///D:/work/learn/Leno/node_modules/.pnpm/@vitejs+plugin-vue@5.2.4_vi_af122e3d9e27fec923ab2c100af821c7/node_modules/@vitejs/plugin-vue/dist/index.mjs";
import path from "node:path";
import { fileURLToPath } from "node:url";
var __vite_injected_original_import_meta_url = "file:///D:/work/learn/Leno/web/buyer-app/vite.config.ts";
var __dirname = path.dirname(fileURLToPath(__vite_injected_original_import_meta_url));
var vite_config_default = defineConfig({
  plugins: [vue()],
  resolve: {
    alias: {
      "@": path.resolve(__dirname, "src")
    }
  },
  server: {
    port: 5176,
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
          vant: ["vant"]
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
        // 仅统计测试运行时实际加载的文件（视图页由浏览器冒烟覆盖，不计入单测覆盖率）
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
//# sourceMappingURL=data:application/json;base64,ewogICJ2ZXJzaW9uIjogMywKICAic291cmNlcyI6IFsidml0ZS5jb25maWcudHMiXSwKICAic291cmNlc0NvbnRlbnQiOiBbImNvbnN0IF9fdml0ZV9pbmplY3RlZF9vcmlnaW5hbF9kaXJuYW1lID0gXCJEOlxcXFx3b3JrXFxcXGxlYXJuXFxcXExlbm9cXFxcd2ViXFxcXGJ1eWVyLWFwcFwiO2NvbnN0IF9fdml0ZV9pbmplY3RlZF9vcmlnaW5hbF9maWxlbmFtZSA9IFwiRDpcXFxcd29ya1xcXFxsZWFyblxcXFxMZW5vXFxcXHdlYlxcXFxidXllci1hcHBcXFxcdml0ZS5jb25maWcudHNcIjtjb25zdCBfX3ZpdGVfaW5qZWN0ZWRfb3JpZ2luYWxfaW1wb3J0X21ldGFfdXJsID0gXCJmaWxlOi8vL0Q6L3dvcmsvbGVhcm4vTGVuby93ZWIvYnV5ZXItYXBwL3ZpdGUuY29uZmlnLnRzXCI7aW1wb3J0IHsgZGVmaW5lQ29uZmlnIH0gZnJvbSAndml0ZSdcclxuaW1wb3J0IHZ1ZSBmcm9tICdAdml0ZWpzL3BsdWdpbi12dWUnXHJcbmltcG9ydCBwYXRoIGZyb20gJ25vZGU6cGF0aCdcclxuaW1wb3J0IHsgZmlsZVVSTFRvUGF0aCB9IGZyb20gJ25vZGU6dXJsJ1xyXG5cclxuY29uc3QgX19kaXJuYW1lID0gcGF0aC5kaXJuYW1lKGZpbGVVUkxUb1BhdGgoaW1wb3J0Lm1ldGEudXJsKSlcclxuXHJcbmV4cG9ydCBkZWZhdWx0IGRlZmluZUNvbmZpZyh7XHJcbiAgcGx1Z2luczogW3Z1ZSgpXSxcclxuICByZXNvbHZlOiB7XHJcbiAgICBhbGlhczoge1xyXG4gICAgICAnQCc6IHBhdGgucmVzb2x2ZShfX2Rpcm5hbWUsICdzcmMnKSxcclxuICAgIH0sXHJcbiAgfSxcclxuICBzZXJ2ZXI6IHtcclxuICAgIHBvcnQ6IDUxNzYsXHJcbiAgICBwcm94eToge1xyXG4gICAgICAnL2FwaSc6IHtcclxuICAgICAgICB0YXJnZXQ6ICdodHRwOi8vbG9jYWxob3N0OjUwMDEnLFxyXG4gICAgICAgIGNoYW5nZU9yaWdpbjogdHJ1ZSxcclxuICAgICAgfSxcclxuICAgIH0sXHJcbiAgfSxcclxuICBidWlsZDoge1xyXG4gICAgdGFyZ2V0OiAnZXMyMDIyJyxcclxuICAgIHNvdXJjZW1hcDogdHJ1ZSxcclxuICAgIHJvbGx1cE9wdGlvbnM6IHtcclxuICAgICAgb3V0cHV0OiB7XHJcbiAgICAgICAgbWFudWFsQ2h1bmtzOiB7XHJcbiAgICAgICAgICB2dWU6IFsndnVlJywgJ3Z1ZS1yb3V0ZXInLCAncGluaWEnXSxcclxuICAgICAgICAgIHZhbnQ6IFsndmFudCddLFxyXG4gICAgICAgIH0sXHJcbiAgICAgIH0sXHJcbiAgICB9LFxyXG4gIH0sXHJcbiAgdGVzdDoge1xyXG4gICAgZW52aXJvbm1lbnQ6ICdqc2RvbScsXHJcbiAgICBnbG9iYWxzOiB0cnVlLFxyXG4gICAgc2V0dXBGaWxlczogJy4vdGVzdHMvc2V0dXAudHMnLFxyXG4gICAgZXhjbHVkZTogW1xyXG4gICAgICAnKiovbm9kZV9tb2R1bGVzLyoqJyxcclxuICAgICAgJyoqL2Rpc3QvKionLFxyXG4gICAgICAnKiovY3lwcmVzcy8qKicsXHJcbiAgICAgICcqKi8ue2lkZWEsZ2l0LGNhY2hlLG91dHB1dCx0ZW1wfS8qKicsXHJcbiAgICAgICcqKi97a2FybWEscm9sbHVwLHdlYnBhY2ssdml0ZSx2aXRlc3QsamVzdCxhdmEsYmFiZWwsbnljLGN5cHJlc3MsdHN1cCxidWlsZH0uY29uZmlnLionLFxyXG4gICAgICAndGVzdHMvZTJlLyoqJyxcclxuICAgIF0sXHJcbiAgICBjb3ZlcmFnZToge1xyXG4gICAgICBwcm92aWRlcjogJ3Y4JyxcclxuICAgICAgcmVwb3J0ZXI6IFsndGV4dCcsICdodG1sJywgJ2pzb24tc3VtbWFyeSddLFxyXG4gICAgICBpbmNsdWRlOiBbJ3NyYy8qKi8qLnRzJywgJ3NyYy8qKi8qLnZ1ZSddLFxyXG4gICAgICBleGNsdWRlOiBbXHJcbiAgICAgICAgJ3NyYy8qKi8qLnNwZWMudHMnLFxyXG4gICAgICAgICdzcmMvbWFpbi50cycsXHJcbiAgICAgICAgJ3NyYy9hcHAvcHJvdmlkZXIudnVlJyxcclxuICAgICAgICAvLyBcdTRFQzVcdTdFREZcdThCQTFcdTZENEJcdThCRDVcdThGRDBcdTg4NENcdTY1RjZcdTVCOUVcdTk2NDVcdTUyQTBcdThGN0RcdTc2ODRcdTY1ODdcdTRFRjZcdUZGMDhcdTg5QzZcdTU2RkVcdTk4NzVcdTc1MzFcdTZENEZcdTg5QzhcdTU2NjhcdTUxOTJcdTcwREZcdTg5ODZcdTc2RDZcdUZGMENcdTRFMERcdThCQTFcdTUxNjVcdTUzNTVcdTZENEJcdTg5ODZcdTc2RDZcdTczODdcdUZGMDlcclxuICAgICAgICAvLyBhbGw6IGZhbHNlIFx1NjVGNlx1NjcyQVx1ODhBQlx1NEVGQlx1NEY1NVx1NkQ0Qlx1OEJENVx1NTJBMFx1OEY3RFx1NzY4NFx1NjU4N1x1NEVGNlx1RkYwOFx1NTk4Mlx1NTQwNCB2aWV3cyBcdTk4NzVcdTk3NjJcdTMwMDFtb2NrIFx1NjU3MFx1NjM2RVx1NzlDRFx1NUI1MFx1RkYwOVx1NEUwRFx1OEZEQlx1NTE2NVx1NjJBNVx1NTQ0QVxyXG4gICAgICAgICdzcmMvc2hhcmVkL2h0dHAvbW9jay9kYXRhLyoqJyxcclxuICAgICAgICAnc3JjLyoqL3R5cGVzLyoqJyxcclxuICAgICAgICAnc3JjLyoqLyouZHRvLnRzJyxcclxuICAgICAgXSxcclxuICAgICAgYWxsOiBmYWxzZSxcclxuICAgICAgdGhyZXNob2xkczoge1xyXG4gICAgICAgIGxpbmVzOiA3MCxcclxuICAgICAgICBmdW5jdGlvbnM6IDcwLFxyXG4gICAgICAgIGJyYW5jaGVzOiA2MCxcclxuICAgICAgICBzdGF0ZW1lbnRzOiA3MCxcclxuICAgICAgfSxcclxuICAgIH0sXHJcbiAgfSxcclxufSlcclxuIl0sCiAgIm1hcHBpbmdzIjogIjtBQUE4UixTQUFTLG9CQUFvQjtBQUMzVCxPQUFPLFNBQVM7QUFDaEIsT0FBTyxVQUFVO0FBQ2pCLFNBQVMscUJBQXFCO0FBSHFKLElBQU0sMkNBQTJDO0FBS3BPLElBQU0sWUFBWSxLQUFLLFFBQVEsY0FBYyx3Q0FBZSxDQUFDO0FBRTdELElBQU8sc0JBQVEsYUFBYTtBQUFBLEVBQzFCLFNBQVMsQ0FBQyxJQUFJLENBQUM7QUFBQSxFQUNmLFNBQVM7QUFBQSxJQUNQLE9BQU87QUFBQSxNQUNMLEtBQUssS0FBSyxRQUFRLFdBQVcsS0FBSztBQUFBLElBQ3BDO0FBQUEsRUFDRjtBQUFBLEVBQ0EsUUFBUTtBQUFBLElBQ04sTUFBTTtBQUFBLElBQ04sT0FBTztBQUFBLE1BQ0wsUUFBUTtBQUFBLFFBQ04sUUFBUTtBQUFBLFFBQ1IsY0FBYztBQUFBLE1BQ2hCO0FBQUEsSUFDRjtBQUFBLEVBQ0Y7QUFBQSxFQUNBLE9BQU87QUFBQSxJQUNMLFFBQVE7QUFBQSxJQUNSLFdBQVc7QUFBQSxJQUNYLGVBQWU7QUFBQSxNQUNiLFFBQVE7QUFBQSxRQUNOLGNBQWM7QUFBQSxVQUNaLEtBQUssQ0FBQyxPQUFPLGNBQWMsT0FBTztBQUFBLFVBQ2xDLE1BQU0sQ0FBQyxNQUFNO0FBQUEsUUFDZjtBQUFBLE1BQ0Y7QUFBQSxJQUNGO0FBQUEsRUFDRjtBQUFBLEVBQ0EsTUFBTTtBQUFBLElBQ0osYUFBYTtBQUFBLElBQ2IsU0FBUztBQUFBLElBQ1QsWUFBWTtBQUFBLElBQ1osU0FBUztBQUFBLE1BQ1A7QUFBQSxNQUNBO0FBQUEsTUFDQTtBQUFBLE1BQ0E7QUFBQSxNQUNBO0FBQUEsTUFDQTtBQUFBLElBQ0Y7QUFBQSxJQUNBLFVBQVU7QUFBQSxNQUNSLFVBQVU7QUFBQSxNQUNWLFVBQVUsQ0FBQyxRQUFRLFFBQVEsY0FBYztBQUFBLE1BQ3pDLFNBQVMsQ0FBQyxlQUFlLGNBQWM7QUFBQSxNQUN2QyxTQUFTO0FBQUEsUUFDUDtBQUFBLFFBQ0E7QUFBQSxRQUNBO0FBQUE7QUFBQTtBQUFBLFFBR0E7QUFBQSxRQUNBO0FBQUEsUUFDQTtBQUFBLE1BQ0Y7QUFBQSxNQUNBLEtBQUs7QUFBQSxNQUNMLFlBQVk7QUFBQSxRQUNWLE9BQU87QUFBQSxRQUNQLFdBQVc7QUFBQSxRQUNYLFVBQVU7QUFBQSxRQUNWLFlBQVk7QUFBQSxNQUNkO0FBQUEsSUFDRjtBQUFBLEVBQ0Y7QUFDRixDQUFDOyIsCiAgIm5hbWVzIjogW10KfQo=

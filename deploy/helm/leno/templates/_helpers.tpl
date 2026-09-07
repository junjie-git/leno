{{/*
展开 chart 全限定名（与 docker-compose 服务名一致）
*/}}
{{- define "leno.fullname" -}}
{{- if .Values.global.nameOverride -}}
{{- .Values.global.nameOverride | trunc 63 | trimSuffix "-" -}}
{{- else -}}
{{- .Release.Name | trunc 63 | trimSuffix "-" -}}
{{- end -}}
{{- end -}}

{{/*
生成服务全限定名：${release}-${serviceName}
*/}}
{{- define "leno.serviceName" -}}
{{- printf "%s-%s" (include "leno.fullname" .context) .name | trunc 63 | trimSuffix "-" -}}
{{- end -}}

{{/*
通用标签
*/}}
{{- define "leno.labels" -}}
app.kubernetes.io/name: {{ .name }}
app.kubernetes.io/instance: {{ .context.Release.Name }}
app.kubernetes.io/managed-by: {{ .context.Release.Service }}
app.kubernetes.io/part-of: leno
{{- end -}}

{{/*
Pod 选取器标签（仅 name + instance）
*/}}
{{- define "leno.selectorLabels" -}}
app.kubernetes.io/name: {{ .name }}
app.kubernetes.io/instance: {{ .context.Release.Name }}
{{- end -}}

{{/*
镜像全限定地址：<registry/>[<namespace/>]<repository>:<tag>
- registry：global.imageRegistry（默认空 = 本地镜像名，如 docker-compose 场景）
- namespace：global.imageNamespace（如 GHCR owner，默认空）
- tag 解析优先级：services.<name>.image.tag > global.imageTag > "latest"
  （CD 通过 --set global.imageTag=<tag> 统一注入；服务级可覆盖）
*/}}
{{- define "leno.image" -}}
{{- $image := .service.image.repository -}}
{{- $namespace := .context.Values.global.imageNamespace | default "" -}}
{{- $registry := .context.Values.global.imageRegistry | default "" -}}
{{- $tag := .service.image.tag | default .context.Values.global.imageTag | default "latest" -}}
{{- if $namespace -}}
{{- $image = printf "%s/%s" $namespace $image -}}
{{- end -}}
{{- if $registry -}}
{{- $image = printf "%s/%s" $registry $image -}}
{{- end -}}
{{- printf "%s:%s" $image $tag -}}
{{- end -}}

{{/*
服务名转 PascalCase 用于 EF migration 项目路径（如 userauth -> Userauth）
*/}}
{{- define "leno.pascalName" -}}
{{- .name | title -}}
{{- end -}}

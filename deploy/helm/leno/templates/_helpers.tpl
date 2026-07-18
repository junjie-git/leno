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
镜像全限定地址：${registry}/${repository}:${tag}
*/}}
{{- define "leno.image" -}}
{{- $registry := .context.Values.global.imageRegistry -}}
{{- if $registry -}}
{{- printf "%s/%s:%s" $registry .service.image.repository .service.image.tag -}}
{{- else -}}
{{- printf "%s:%s" .service.image.repository .service.image.tag -}}
{{- end -}}
{{- end -}}

{{/*
服务名转 PascalCase 用于 EF migration 项目路径（如 userauth -> Userauth）
*/}}
{{- define "leno.pascalName" -}}
{{- .name | title -}}
{{- end -}}

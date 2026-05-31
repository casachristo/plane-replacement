{{/* Generate common labels */}}
{{- define "waypoint.labels" -}}
app.kubernetes.io/name: waypoint
app.kubernetes.io/instance: {{ .Release.Name }}
app.kubernetes.io/version: {{ .Chart.AppVersion | quote }}
app.kubernetes.io/managed-by: {{ .Release.Service }}
{{- end -}}

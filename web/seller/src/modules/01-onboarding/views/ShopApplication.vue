<script setup lang="ts">
import { ref, reactive, computed } from 'vue'
import { useRouter } from 'vue-router'
import {
  Breadcrumb,
  BreadcrumbItem,
  Card,
  Steps,
  Step,
  Form,
  FormItem,
  Input,
  Select,
  Button,
  Space,
  Descriptions,
  DescriptionsItem,
  message,
  Modal,
} from 'ant-design-vue'
import { shopApi } from '../api/shop.api'
import type { ShopApplicationDto } from '../types/shop.dto'
import { IdempotencyButton } from '@/shared/components'
import { logger } from '@/shared/utils/logger'
import { ConcurrencyError } from '@/shared/http'

/**
 * 入驻申请页
 *
 * 路由 /shop/application，权限 shop:application:submit，requiresActiveShop: false
 * 三步式表单：1) 基础信息 2) 确认信息 3) 提交（IdempotencyButton + 幂等键）
 * 成功后跳转 /shop/qualifications。
 */

const router = useRouter()

const current = ref(0)
const submitting = ref(false)
const formRef = ref()

const form = reactive({
  name: '',
  mainCategory: '' as string,
  description: '',
  contactPhone: '',
  contactEmail: '',
})

const rules = {
  name: [
    { required: true, message: '请输入店铺名称', trigger: 'blur' },
    { min: 2, max: 32, message: '店铺名称长度为 2-32 字', trigger: 'blur' },
  ],
  mainCategory: [{ required: true, message: '请选择主营类目', trigger: 'change' }],
  contactPhone: [
    { required: true, message: '请输入联系电话', trigger: 'blur' },
    { pattern: /^1[3-9]\d{9}$/, message: '请输入有效的手机号', trigger: 'blur' },
  ],
  contactEmail: [{ type: 'email', message: '邮箱格式不正确', trigger: 'blur' }],
  description: [{ max: 500, message: '描述最长 500 字', trigger: 'blur' }],
}

const categoryOptions: Array<{ label: string; value: string }> = [
  { label: '服装', value: '服装' },
  { label: '数码', value: '数码' },
  { label: '家居', value: '家居' },
  { label: '美妆', value: '美妆' },
  { label: '食品', value: '食品' },
  { label: '母婴', value: '母婴' },
  { label: '其他', value: '其他' },
]

const canNext = computed(() => {
  return (
    form.name.trim().length >= 2 &&
    form.name.trim().length <= 32 &&
    !!form.mainCategory &&
    /^1[3-9]\d{9}$/.test(form.contactPhone)
  )
})

async function next(): Promise<void> {
  try {
    await formRef.value?.validate()
    current.value = 1
  } catch {
    message.warning('请完善必填项后再进入下一步')
  }
}

function prev(): void {
  current.value = 0
}

function buildBody(): ShopApplicationDto {
  const body: ShopApplicationDto = {
    name: form.name.trim(),
    mainCategory: form.mainCategory,
    contactPhone: form.contactPhone.trim(),
  }
  if (form.description.trim()) body.description = form.description.trim()
  if (form.contactEmail.trim()) body.contactEmail = form.contactEmail.trim()
  return body
}

function resetForm(): void {
  form.name = ''
  form.mainCategory = ''
  form.description = ''
  form.contactPhone = ''
  form.contactEmail = ''
  current.value = 0
}

function handleConcurrencyError(): void {
  Modal.confirm({
    title: '资源冲突',
    content: '检测到已存在入驻申请或店铺信息已变更，是否重置表单后重新填写？',
    okText: '重置表单',
    cancelText: '返回首页',
    onOk: () => {
      resetForm()
    },
    onCancel: () => {
      router.push('/dashboard/overview')
    },
  })
}

async function onSubmit(): Promise<void> {
  submitting.value = true
  try {
    await shopApi.submitApplication(buildBody())
    message.success('入驻申请已提交，请继续上传资质文件')
    router.push('/shop/qualifications')
  } catch (e) {
    logger.error('提交入驻申请失败', e)
    if (e instanceof ConcurrencyError) {
      handleConcurrencyError()
    } else {
      message.error('提交失败，请稍后重试')
    }
  } finally {
    submitting.value = false
  }
}
</script>

<template>
  <div class="shop-application-page">
    <Breadcrumb class="shop-application-breadcrumb">
      <BreadcrumbItem>店铺设置</BreadcrumbItem>
      <BreadcrumbItem>入驻申请</BreadcrumbItem>
    </Breadcrumb>

    <Card class="shop-application-card" :bordered="true">
      <Steps :current="current" class="shop-application-steps">
        <Step title="基础信息" description="填写店铺与联系方式" />
        <Step title="确认信息" description="核对填写内容" />
        <Step title="提交申请" description="提交后进入资质上传" />
      </Steps>

      <!-- 步骤 1：基础信息 -->
      <div v-if="current === 0" class="shop-application-step-body">
        <Form
          ref="formRef"
          :model="form"
          :rules="rules"
          layout="vertical"
          :label-col="{ style: { width: '120px' } }"
        >
          <FormItem label="店铺名称" name="name" required>
            <Input
              v-model:value="form.name"
              placeholder="请输入店铺名称（2-32 字）"
              :maxlength="32"
              show-count
            />
          </FormItem>
          <FormItem label="主营类目" name="mainCategory" required>
            <Select
              v-model:value="form.mainCategory"
              placeholder="请选择主营类目"
              :options="categoryOptions"
            />
          </FormItem>
          <FormItem label="店铺描述" name="description">
            <Input
              v-model:value="form.description"
              type="textarea"
              :rows="4"
              placeholder="请输入店铺描述（选填，最长 500 字）"
              :maxlength="500"
              show-count
            />
          </FormItem>
          <FormItem label="联系电话" name="contactPhone" required>
            <Input
              v-model:value="form.contactPhone"
              placeholder="请输入手机号"
              :maxlength="11"
            />
          </FormItem>
          <FormItem label="联系邮箱" name="contactEmail">
            <Input
              v-model:value="form.contactEmail"
              placeholder="请输入邮箱（选填）"
            />
          </FormItem>
        </Form>
        <div class="shop-application-actions">
          <Button type="primary" :disabled="!canNext" @click="next">下一步</Button>
        </div>
      </div>

      <!-- 步骤 2：确认信息 -->
      <div v-else-if="current === 1" class="shop-application-step-body">
        <Descriptions :column="1" bordered>
          <DescriptionsItem label="店铺名称">{{ form.name }}</DescriptionsItem>
          <DescriptionsItem label="主营类目">{{ form.mainCategory }}</DescriptionsItem>
          <DescriptionsItem label="店铺描述">{{ form.description || '—' }}</DescriptionsItem>
          <DescriptionsItem label="联系电话">{{ form.contactPhone }}</DescriptionsItem>
          <DescriptionsItem label="联系邮箱">{{ form.contactEmail || '—' }}</DescriptionsItem>
        </Descriptions>
        <div class="shop-application-actions">
          <Space>
            <Button @click="prev">上一步</Button>
            <Button type="primary" @click="current = 2">下一步</Button>
          </Space>
        </div>
      </div>

      <!-- 步骤 3：提交 -->
      <div v-else class="shop-application-step-body">
        <div class="shop-application-confirm-text">
          请确认以上信息无误，点击提交后将进入资质上传环节。
        </div>
        <div class="shop-application-actions">
          <Space>
            <Button @click="prev">上一步</Button>
            <IdempotencyButton :loading="submitting" @click="onSubmit">
              提交申请
            </IdempotencyButton>
          </Space>
        </div>
      </div>
    </Card>
  </div>
</template>

<style scoped>
.shop-application-page {
  display: flex;
  flex-direction: column;
  gap: 16px;
}
.shop-application-breadcrumb {
  font-size: 14px;
}
.shop-application-card {
  border-radius: 8px;
}
.shop-application-steps {
  margin-bottom: 32px;
}
.shop-application-step-body {
  max-width: 640px;
  margin: 0 auto;
}
.shop-application-actions {
  margin-top: 24px;
  display: flex;
  justify-content: flex-end;
}
.shop-application-confirm-text {
  padding: 24px;
  background: #fafafa;
  border-radius: 6px;
  color: #595959;
  font-size: 14px;
  line-height: 1.8;
}
</style>

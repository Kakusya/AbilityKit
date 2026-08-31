<template>
  <section id="skill-acceptance" class="card span-12 acceptance-panel">
    <div class="card-head"><div><p class="section-kicker">场景产物</p><h2>场景验收报告</h2></div><span class="badge">{{ admin.acceptanceBatch.value?.cases?.length || 0 }} 个用例</span></div>
    <div class="acceptance-toolbar row"><div><label>产物目录</label><input v-model="admin.acceptance.artifactDirectory" placeholder="artifacts/moba-acceptance" /></div><div><label>追踪上限</label><input v-model.number="admin.acceptance.traceLimit" type="number" /></div></div>
    <div class="artifact-directory-strip">
      <article v-for="directory in admin.acceptanceArtifactDirectories.value?.directories || []" :key="directory.artifactDirectory" class="artifact-directory-card" :class="{ active: directory.artifactDirectory === admin.acceptance.artifactDirectory, missing: !directory.exists }" @click="admin.selectAcceptanceArtifactDirectory(directory.artifactDirectory)">
        <strong>{{ directory.displayName || directory.artifactDirectory }}</strong>
        <small>{{ directory.artifactDirectory }}</small>
        <span>{{ directory.exists ? `${directory.caseCount} 个用例 / ${directory.hasBatchSummary ? '有批次' : '无批次'}` : '目录缺失' }}</span>
      </article>
    </div>
    <div class="acceptance-filters row"><div><label>搜索用例 / 描述 / 路径 / 标签</label><input v-model="admin.acceptance.searchText" placeholder="例如：projectile、damage、case id" /></div><div><label>Category</label><select v-model="admin.acceptance.categoryFilter"><option value="">全部</option><option value="contract">contract</option><option value="golden">golden</option><option value="draft">draft</option></select></div><div><label>Tag</label><input v-model="admin.acceptance.tagFilter" placeholder="例如：buff、projectile、core" /></div><div><label>状态过滤</label><select v-model="admin.acceptance.statusFilter"><option value="all">全部</option><option value="failed">失败优先排查</option><option value="passed">仅通过</option><option value="unknown">未知</option></select></div><div><label>排序</label><select v-model="admin.acceptance.sortKey"><option value="caseId">用例 ID</option><option value="failedFirst">失败优先</option><option value="duration">耗时</option><option value="trace">追踪数量</option><option value="status">状态</option></select></div></div>
    <div class="acceptance-runner-panel">
      <div class="trace-section-head"><h3>真实英雄 DSL 执行</h3><span class="badge">Unity EditMode</span></div>
      <p class="muted">选择已提交的英雄场景后，后台会通过固定脚本启动 Unity DSL 执行器。浏览器不能提交命令、路径或任意 DSL；每次运行都会产生独立的 summary、trace JSONL 与 Unity 日志。</p>
      <div class="runner-template-grid">
        <article v-for="template in admin.acceptanceTemplates.value?.templates || []" :key="template.id" class="runner-template-card" :class="{ active: template.id === admin.acceptance.selectedTemplateId }" @click="admin.applyAcceptanceTemplate(template.id)">
          <div><strong>{{ template.displayName }}</strong><span class="badge">{{ template.id }}</span></div>
          <p>{{ template.description }}</p>
          <small>{{ template.covers.join(' / ') }}</small>
        </article>
      </div>
      <div class="runner-form-grid">
        <div><label>受控场景 ID</label><input :value="admin.acceptance.selectedTemplateId" readonly /></div>
        <div><label>目标用例</label><input :value="admin.acceptance.runCaseId" readonly /></div>
        <div><label>技能 ID</label><input :value="admin.acceptance.runSkillId" readonly /></div>
        <div><label>场景说明</label><input v-model="admin.acceptance.runDescription" readonly /></div>
      </div>
      <label>操作原因</label><input v-model="admin.acceptance.runOperatorReason" />
      <div v-if="admin.acceptanceLastRun.value" class="status-box" :class="{ failed: !admin.acceptanceLastRun.value.success }"><strong>最近运行：{{ admin.acceptanceLastRun.value.caseId }} / {{ admin.acceptanceLastRun.value.executionStatus }}</strong><span>场景：{{ admin.acceptanceLastRun.value.scenarioId }} / 退出码：{{ admin.acceptanceLastRun.value.exitCode }} / {{ admin.acceptanceLastRun.value.durationMs }}ms</span><span>摘要：{{ admin.acceptanceLastRun.value.summaryPath || '未生成' }}</span><span>追踪：{{ admin.acceptanceLastRun.value.tracePath || '未生成' }}</span><span>日志：{{ admin.acceptanceLastRun.value.logPath || '未生成' }}</span><span>执行结果：{{ admin.acceptanceLastRun.value.executionResultPath || '未生成' }}</span></div>
    </div>
    <div class="actions action-bar"><button class="success" :disabled="admin.busy.value" @click="admin.runAcceptanceAnalysis">运行真实 DSL 场景</button><button class="secondary" :disabled="admin.busy.value" @click="admin.refreshAcceptanceArtifacts">刷新场景产物</button><button class="secondary" :disabled="admin.busy.value" @click="admin.refreshAcceptanceRunPlan">查看执行入口边界</button></div>
    <div class="ops-status acceptance-summary"><div><span>产物目录</span><strong>{{ admin.acceptanceBatch.value?.artifactDirectory || admin.acceptance.artifactDirectory }}</strong></div><div><span>批次摘要</span><strong>{{ admin.acceptanceBatch.value?.hasBatchSummary ? '已找到' : '缺失' }}</strong></div><div><span>通过</span><strong>{{ admin.acceptancePassedCount.value }}</strong></div><div><span>失败</span><strong>{{ admin.acceptanceFailedCount.value }}</strong></div><div><span>未知</span><strong>{{ admin.acceptanceUnknownCount.value }}</strong></div><div><span>过滤后</span><strong>{{ admin.acceptanceFilteredCases.value.length }}</strong></div></div>
    <div class="acceptance-layout">
      <div class="acceptance-case-list">
        <div class="acceptance-list-toolbar">
          <h3>用例列表</h3>
          <div class="actions">
            <span class="badge">已选 {{ admin.acceptanceSelectedCount.value }}</span>
            <button class="secondary" :disabled="admin.busy.value || !admin.acceptanceFilteredCases.value.length" @click="admin.selectAllFilteredAcceptanceCases">全选过滤结果</button>
            <button class="secondary" :disabled="admin.busy.value || !admin.acceptanceSelectedCount.value" @click="admin.clearAcceptanceCaseSelection">清空选择</button>
            <button class="danger" :disabled="admin.busy.value || !admin.acceptanceSelectedCount.value" @click="admin.deleteAcceptanceCases()">删除选中</button>
            <button class="secondary" :disabled="admin.busy.value" @click="admin.refreshAcceptanceArtifacts">刷新列表</button>
          </div>
        </div>
        <article v-for="item in admin.acceptanceFilteredCases.value" :key="item.caseId" class="acceptance-case" :class="{ active: item.caseId === admin.acceptance.selectedCaseId, selected: admin.isAcceptanceCaseSelected(item.caseId), failed: item.passed === false }" @click="admin.refreshAcceptanceCase(item.caseId)">
          <label class="acceptance-case-select" @click.stop>
            <input type="checkbox" :checked="admin.isAcceptanceCaseSelected(item.caseId)" @change="admin.toggleAcceptanceCaseSelection(item.caseId)" />
          </label>
          <div class="acceptance-case-main"><div><strong>{{ item.caseId }}</strong><small>{{ item.description || item.worldId || item.summaryPath }}</small></div><p><span class="badge">{{ item.category || 'contract' }}</span> {{ (item.tags || []).join(' / ') || 'no-tags' }}</p><p>帧 {{ item.finalFrame }} / {{ item.finalTimeMs }}ms / 追踪 {{ item.traceNodeCount }}</p><p v-if="item.missingTraceNodes || item.missingActions || item.missingRelationships" class="muted">缺口：{{ item.missingTraceNodes || 'trace ok' }} / {{ item.missingActions || 'actions ok' }} / {{ item.missingRelationships || 'relations ok' }}</p></div>
          <div class="acceptance-case-actions">
            <span class="badge" :class="item.passed === false ? 'danger' : ''">{{ item.passed === false ? '失败' : item.passed === true ? '通过' : '未知' }}</span>
            <button class="danger" :disabled="admin.busy.value" @click.stop="admin.deleteAcceptanceCase(item.caseId)">删除</button>
          </div>
        </article>
        <p v-if="!admin.acceptanceBatch.value?.cases?.length" class="muted">尚未读取到场景产物；先通过 Unity、单元测试或 CI 生成 batch_summary.json 与 *_trace.jsonl。</p>
        <p v-else-if="!admin.acceptanceFilteredCases.value.length" class="muted">当前过滤条件下没有匹配的用例。</p>
      </div>
      <div class="acceptance-detail">
        <h3>用例摘要</h3>
        <div v-if="admin.acceptanceCase.value" class="status-box"><strong>{{ admin.acceptanceCase.value.caseId }}</strong><span>{{ admin.acceptanceCase.value.summaryPath }}</span><span>{{ admin.acceptanceCase.value.tracePath }}</span></div>
        <ul class="diagnostic-list warnings"><li v-for="warning in [...(admin.acceptanceBatch.value?.warnings || []), ...(admin.acceptanceCase.value?.warnings || [])]" :key="warning">{{ warning }}</li><li v-if="!admin.acceptanceBatch.value?.warnings?.length && !admin.acceptanceCase.value?.warnings?.length">暂无 artifact 告警。</li></ul>
        <h3>断言分组</h3>
        <div class="assertion-groups">
          <article v-for="group in admin.acceptanceAssertionGroups.value" :key="group.key" class="assertion-group">
            <strong>{{ group.title }}</strong>
            <ul><li v-for="item in group.items" :key="item">{{ item }}</li></ul>
          </article>
        </div>
        <SkillTraceVisualizer title="统一追踪树" :nodes="admin.acceptanceAnalysisFilteredFlat.value" :total-node-count="admin.acceptanceAnalysisFlat.value.length" :selected-node="admin.selectedAcceptanceAnalysisNode.value" :timeline="admin.acceptanceAnalysisTimeline.value" :filter="admin.acceptanceAnalysisFilter" :filter-options="admin.acceptanceAnalysisFilterOptions.value" :entity-relations="admin.acceptanceAnalysisEntityRelations.value" empty-text="选择用例后显示根据统一节点投影构建的追踪树。" @select-node="admin.selectAcceptanceAnalysisNode" />
        <h3>追踪 JSONL 预览</h3>
        <div class="trace-preview"><article v-for="(record, index) in admin.acceptanceTracePreview.value" :key="index" class="trace-record"><strong>#{{ index + 1 }}</strong><pre>{{ JSON.stringify(record, null, 2) }}</pre></article><p v-if="!admin.acceptanceTracePreview.value.length" class="muted">选择用例后显示追踪记录预览。</p></div>
        <h3>执行入口边界</h3>
        <div v-if="admin.acceptanceRunPlan.value" class="run-boundary">
          <div class="status-box"><strong>{{ admin.acceptanceRunPlan.value.allowed ? '可执行' : '只读模式' }} / {{ admin.acceptanceRunPlan.value.executionMode }}</strong><span>{{ admin.acceptanceRunPlan.value.message }}</span><span>产物目录：{{ admin.acceptanceRunPlan.value.artifactDirectory }}</span><span>后台请求：{{ admin.acceptanceRunPlan.value.canRequestFromAdmin ? '已启用' : '已禁用' }}</span></div>
          <div class="run-boundary-grid">
            <article v-for="strategy in admin.acceptanceRunPlan.value.strategies" :key="strategy.id" class="run-boundary-card"><div><strong>{{ strategy.displayName }}</strong><span class="badge">{{ strategy.status }}</span></div><p>{{ strategy.description }}</p><small>{{ strategy.boundary }}</small></article>
          </div>
          <h4>白名单脚本 / 作业</h4>
          <article v-for="script in admin.acceptanceRunPlan.value.allowedScripts" :key="script.id" class="allowed-script"><div><strong>{{ script.displayName }}</strong><span class="badge" :class="script.exists ? '' : 'danger'">{{ script.exists ? '已找到' : '占位' }}</span></div><small>{{ script.id }} / {{ script.shell }} / {{ script.relativePath }}</small><p>参数：{{ script.arguments.join(' | ') }}</p><p>产物：{{ script.produces.join(', ') }}</p></article>
          <div class="run-boundary-grid compact"><article><strong>必需审批</strong><ul><li v-for="item in admin.acceptanceRunPlan.value.requiredApprovals" :key="item">{{ item }}</li></ul></article><article><strong>审计字段</strong><ul><li v-for="item in admin.acceptanceRunPlan.value.auditFields" :key="item">{{ item }}</li></ul></article></div>
          <ul class="diagnostic-list"><li v-for="note in admin.acceptanceRunPlan.value.notes" :key="note">{{ note }}</li></ul>
        </div>
      </div>
    </div>
  </section>
</template>

<script setup lang="ts">
import SkillTraceVisualizer from './SkillTraceVisualizer.vue';
import { useAdminConsoleStore } from '../../stores/adminConsoleStore';

const admin = useAdminConsoleStore();
</script>

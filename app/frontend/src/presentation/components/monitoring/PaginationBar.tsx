import { Button, Group, NumberInput, Text } from '@mantine/core'
import { IconChevronLeft, IconChevronRight } from '@tabler/icons-react'

type Props = {
  limit: number
  offset: number
  pageSize: number
  /** 後端 `total_count`；有值時「下一頁」依總筆數判斷 */
  totalCount?: number
  onLimitChange: (v: number) => void
  onOffsetChange: (v: number) => void
}

export function PaginationBar({
  limit,
  offset,
  pageSize,
  totalCount,
  onLimitChange,
  onOffsetChange,
}: Props) {
  const canPrev = offset > 0
  const canNext =
    totalCount !== undefined
      ? offset + pageSize < totalCount
      : pageSize >= limit && pageSize > 0

  return (
    <Group justify="space-between" align="flex-end" wrap="wrap" gap="sm">
      <Group gap="xs" align="flex-end">
        <NumberInput
          label="每頁筆數"
          min={1}
          max={500}
          value={limit}
          onChange={(v) => onLimitChange(typeof v === 'number' ? v : Number(v) || 50)}
          size="xs"
          w={120}
        />
        <Text size="xs" c="dimmed" mb={4}>
          offset: {offset}
          {totalCount !== undefined ? ` / 總筆數 ${totalCount}` : ''}
        </Text>
      </Group>
      <Group gap="xs">
        <Button
          size="xs"
          variant="light"
          leftSection={<IconChevronLeft size={14} />}
          disabled={!canPrev}
          onClick={() => onOffsetChange(Math.max(0, offset - limit))}
        >
          上一頁
        </Button>
        <Button
          size="xs"
          variant="light"
          rightSection={<IconChevronRight size={14} />}
          disabled={!canNext}
          onClick={() => onOffsetChange(offset + limit)}
        >
          下一頁
        </Button>
      </Group>
    </Group>
  )
}

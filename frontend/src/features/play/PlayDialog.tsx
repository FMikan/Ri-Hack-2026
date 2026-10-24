import { useSearchParams } from 'react-router'
import { Dialog, DialogContent } from '@/components/ui/dialog'
import { BotPicker } from './BotPicker'
import { RankedQueue } from './RankedQueue'

type PlayMode = 'bot' | 'ranked'

// Play opens over whatever page is showing: ?play=bot picks a bot and a topic, ?play=ranked enters the queue.
// Links set the parameter (with replace), so any page can open it and closing never leaves a history entry.
export function PlayDialog() {
  const [params, setParams] = useSearchParams()
  const mode = params.get('play')
  const setMode = (next?: PlayMode) =>
    setParams(
      (previous) => {
        const updated = new URLSearchParams(previous)
        if (next) updated.set('play', next)
        else updated.delete('play')
        return updated
      },
      { replace: true },
    )

  return (
    <Dialog open={mode === 'bot' || mode === 'ranked'} onOpenChange={(open) => !open && setMode()}>
      <DialogContent
        showCloseButton={false}
        className="gap-5 bg-card p-5 sm:max-w-2xl sm:p-6"
      >
        {mode === 'ranked' ? <RankedQueue onPlayBot={() => setMode('bot')} /> : mode === 'bot' && <BotPicker />}
      </DialogContent>
    </Dialog>
  )
}

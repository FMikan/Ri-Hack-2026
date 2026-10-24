import { ChevronDown, FlaskConical, LayoutDashboard, LogOut, Swords, Target, Trophy, UserRound, type LucideIcon } from 'lucide-react'
import { NavLink, Outlet, useLocation } from 'react-router'
import coin from '@/assets/brand/stackoff-coin-on-dark.svg'
import coinSmall from '@/assets/brand/stackoff-coin-small-on-dark.svg'
import wordmark from '@/assets/brand/stackoff-wordmark-on-dark.svg'
import { Wordmark } from '@/components/shared/Wordmark'
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuLabel,
  DropdownMenuRadioGroup,
  DropdownMenuRadioItem,
  DropdownMenuSeparator,
  DropdownMenuTrigger,
} from '@/components/ui/dropdown-menu'
import { Badge } from '@/components/ui/badge'
import { useAuth } from '@/features/auth/auth-context'
import { DEMO_DATA, demoMe } from '@/lib/fixtures/demo'
import { LOCALE_NAMES, useLocale, type Locale } from '@/lib/i18n/locale-context'
import { cn } from '@/lib/utils'

type NavKey = 'dashboard' | 'play' | 'practice' | 'leaderboards' | 'profile'

// Screens without a route yet show as "Soon"; give an item a `to` when its page lands.
const NAV: { key: NavKey; icon: LucideIcon; to?: string }[] = [
  { key: 'dashboard', icon: LayoutDashboard, to: '/' },
  { key: 'play', icon: Swords, to: '/play' },
  { key: 'practice', icon: Target },
  { key: 'leaderboards', icon: Trophy },
  { key: 'profile', icon: UserRound },
]

const focusRing = 'focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-ring'

// Signed-in layout: icon rail on desktop, bottom tab bar on phones, top bar with coins and the account menu.
export function AppShell() {
  return (
    <div className="min-h-svh lg:grid lg:grid-cols-[5rem_minmax(0,1fr)]">
      <SideRail />
      <div className="flex min-w-0 flex-col pb-24 lg:pb-0">
        <TopBar />
        <Outlet />
      </div>
      <BottomNav />
    </div>
  )
}

// Collapsed to icons. Hovering or tabbing into it widens it over the page and shows the full names.
function SideRail() {
  const { t } = useLocale()
  const { pathname } = useLocation()
  const reveal = 'opacity-0 transition-opacity group-focus-within/rail:opacity-100 group-hover/rail:opacity-100'

  return (
    <aside className="group/rail sticky top-0 z-40 hidden h-svh lg:block">
      <div className="absolute inset-y-0 left-0 flex w-20 flex-col gap-8 overflow-hidden border-r border-border bg-sidebar py-5 shadow-black/50 transition-[width,box-shadow] duration-200 ease-out group-focus-within/rail:w-60 group-focus-within/rail:shadow-2xl group-hover/rail:w-60 group-hover/rail:shadow-2xl motion-reduce:transition-none">
        <NavLink to="/" aria-label="StackOff" className={cn('relative mx-5 flex h-10 items-center rounded-sm', focusRing)}>
          <img
            src={coin}
            alt=""
            className="size-10 shrink-0 transition-opacity group-focus-within/rail:opacity-0 group-hover/rail:opacity-0"
          />
          <img src={wordmark} alt="" className={cn('absolute left-0 h-7 w-auto max-w-none', reveal)} />
        </NavLink>
        <nav aria-label={t.shell.mainNav} className="px-3">
          <ul className="grid grid-cols-[minmax(0,1fr)] gap-1.5">
            {NAV.map((item) => {
              const label = t.shell.nav[item.key]
              const base = 'flex h-11 min-w-0 items-center gap-3 overflow-hidden rounded-lg px-4.5 text-sm font-medium whitespace-nowrap'
              const inner = (
                <>
                  <item.icon aria-hidden="true" className="size-5 shrink-0" />
                  <span className={reveal}>{label}</span>
                  {!item.to && <span className={cn('ml-auto font-mono text-xs', reveal)}>{t.shell.soon}</span>}
                </>
              )
              return (
                <li key={item.key}>
                  {item.to ? (
                    <NavLink
                      to={item.to}
                      end
                      className={cn(
                        base,
                        'transition-colors',
                        pathname === item.to
                          ? 'bg-primary/15 text-primary'
                          : 'text-muted-foreground hover:bg-secondary hover:text-foreground',
                        focusRing,
                      )}
                    >
                      {inner}
                    </NavLink>
                  ) : (
                    <button
                      type="button"
                      aria-disabled="true"
                      className={cn(base, 'w-full cursor-not-allowed text-muted-foreground/60', focusRing)}
                    >
                      {inner}
                    </button>
                  )}
                </li>
              )
            })}
          </ul>
        </nav>
      </div>
    </aside>
  )
}

function BottomNav() {
  const { t } = useLocale()

  return (
    <nav
      aria-label={t.shell.mainNav}
      className="fixed inset-x-0 bottom-0 z-40 border-t border-border bg-background pb-[env(safe-area-inset-bottom)] lg:hidden"
    >
      <ul className="mx-auto grid max-w-lg grid-cols-5">
        {NAV.map((item) => {
          const label = t.shell.nav[item.key]
          const inner = (
            <>
              <item.icon aria-hidden="true" className="size-5" />
              <span className="max-w-full truncate text-xs">{label}</span>
            </>
          )
          const base = 'flex h-16 flex-col items-center justify-center gap-1 px-1'
          return (
            <li key={item.key}>
              {item.to ? (
                <NavLink
                  to={item.to}
                  end
                  className={({ isActive }) =>
                    cn(
                      base,
                      // Blue text on navy is too faint, so the active tab is white with a blue bar on top.
                      isActive
                        ? 'relative font-medium text-foreground before:absolute before:inset-x-5 before:top-0 before:h-0.5 before:rounded-full before:bg-primary'
                        : 'text-muted-foreground hover:text-foreground',
                      focusRing,
                    )
                  }
                >
                  {inner}
                </NavLink>
              ) : (
                <span aria-disabled="true" aria-label={`${label} (${t.shell.soon})`} className={cn(base, 'text-muted-foreground/50')}>
                  {inner}
                </span>
              )}
            </li>
          )
        })}
      </ul>
    </nav>
  )
}

function TopBar() {
  const { session, logout } = useAuth()
  const { t, locale, setLocale } = useLocale()
  if (session.status !== 'authenticated') return null
  const user = demoMe(session.user)

  return (
    <header className="mx-auto flex w-full max-w-7xl items-center gap-3 px-4 py-3 sm:px-6 lg:px-8 lg:py-5">
      <NavLink to="/" className={cn('rounded-sm lg:hidden', focusRing)}>
        <Wordmark className="h-6" />
      </NavLink>
      <p className="hidden min-w-0 truncate font-heading text-lg font-semibold tracking-tight lg:block">
        {t.shell.greeting(user.username)}
      </p>
      <div className="ml-auto flex items-center gap-2 sm:gap-3">
        {DEMO_DATA && (
          <Badge variant="outline">
            <FlaskConical aria-hidden="true" />
            {t.shell.demo}
          </Badge>
        )}
        <span className="flex h-9 items-center gap-2 rounded-lg bg-card px-3 font-mono text-sm font-medium">
          <img src={coinSmall} alt="" className="size-4" />
          <span className="sr-only">{t.shell.coins}:</span>
          {user.coins.toLocaleString(locale)}
        </span>
        <DropdownMenu>
          <DropdownMenuTrigger
            aria-label={t.shell.account}
            className={cn('flex items-center gap-2 rounded-full py-1 pr-2 pl-1 transition-colors hover:bg-card', focusRing)}
          >
            <span className="grid size-8 place-items-center rounded-full bg-primary font-heading text-sm font-semibold text-primary-foreground">
              {user.username.charAt(0).toUpperCase()}
            </span>
            <span className="hidden max-w-32 truncate text-sm font-medium sm:block">{user.username}</span>
            <ChevronDown aria-hidden="true" className="size-4 text-muted-foreground" />
          </DropdownMenuTrigger>
          <DropdownMenuContent align="end" className="w-60">
            <DropdownMenuLabel className="grid gap-0.5">
              <span className="truncate">{user.username}</span>
              <span className="truncate text-xs font-normal text-muted-foreground">{user.email}</span>
            </DropdownMenuLabel>
            <DropdownMenuSeparator />
            <DropdownMenuLabel className="text-xs font-normal text-muted-foreground">{t.common.language}</DropdownMenuLabel>
            <DropdownMenuRadioGroup value={locale} onValueChange={(value) => setLocale(value as Locale)}>
              {(Object.keys(LOCALE_NAMES) as Locale[]).map((option) => (
                <DropdownMenuRadioItem key={option} value={option} lang={option}>
                  {LOCALE_NAMES[option]}
                </DropdownMenuRadioItem>
              ))}
            </DropdownMenuRadioGroup>
            <DropdownMenuSeparator />
            <DropdownMenuItem onSelect={logout}>
              <LogOut aria-hidden="true" />
              {t.shell.logOut}
            </DropdownMenuItem>
          </DropdownMenuContent>
        </DropdownMenu>
      </div>
    </header>
  )
}

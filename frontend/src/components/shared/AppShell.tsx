import {
  ChevronDown,
  FlaskConical,
  LayoutDashboard,
  LogOut,
  Menu,
  Settings,
  Swords,
  Target,
  Trophy,
  UserRound,
  type LucideIcon,
} from 'lucide-react'
import { useState } from 'react'
import { NavLink, Outlet, useLocation } from 'react-router'
import coin from '@/assets/brand/stackoff-coin-on-dark.svg'
import coinSmall from '@/assets/brand/stackoff-coin-small-on-dark.svg'
import wordmark from '@/assets/brand/stackoff-wordmark-on-dark.svg'
import { Wordmark } from '@/components/shared/Wordmark'
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuGroup,
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
  { key: 'practice', icon: Target, to: '/practice' },
  { key: 'leaderboards', icon: Trophy, to: '/leaderboards' },
  { key: 'profile', icon: UserRound, to: '/profile' },
]

const focusRing = 'focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-ring'

// Signed-in layout: icon rail on desktop, top bar with coins and the menu. On phones the menu carries the navigation.
export function AppShell() {
  return (
    <div className="min-h-svh lg:grid lg:grid-cols-[5rem_minmax(0,1fr)]">
      <SideRail />
      <div className="flex min-w-0 flex-col">
        <TopBar />
        <Outlet />
      </div>
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

function TopBar() {
  const { session, logout } = useAuth()
  const { t, locale, setLocale } = useLocale()
  const { pathname } = useLocation()
  const [settingsOpen, setSettingsOpen] = useState(false)
  if (session.status !== 'authenticated') return null
  const user = demoMe(session.user)
  const touch = 'max-lg:py-3' // 44px rows on phones, where this menu is the navigation

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
            aria-label={t.shell.menu}
            className={cn(
              'flex shrink-0 items-center gap-2 rounded-lg transition-colors hover:bg-card max-lg:size-11 max-lg:justify-center lg:rounded-full lg:py-1 lg:pr-2 lg:pl-1',
              focusRing,
            )}
          >
            <Menu aria-hidden="true" className="size-6 lg:hidden" />
            <span className="hidden size-8 place-items-center rounded-full bg-primary font-heading text-sm font-semibold text-primary-foreground lg:grid">
              {user.username.charAt(0).toUpperCase()}
            </span>
            <span className="hidden max-w-32 truncate text-sm font-medium lg:block">{user.username}</span>
            <ChevronDown aria-hidden="true" className="hidden size-4 text-muted-foreground lg:block" />
          </DropdownMenuTrigger>
          <DropdownMenuContent align="end" className="w-64 lg:w-60">
            <DropdownMenuLabel className="grid gap-0.5">
              <span className="truncate">{user.username}</span>
              <span className="truncate text-xs font-normal text-muted-foreground">{user.email}</span>
            </DropdownMenuLabel>
            <DropdownMenuSeparator />
            {/* Phones have no rail, so the menu lists the screens too. */}
            <DropdownMenuGroup className="lg:hidden">
              {NAV.map((item) => {
                const label = t.shell.nav[item.key]
                return item.to ? (
                  <DropdownMenuItem key={item.key} asChild className={cn(touch, pathname === item.to && 'text-primary')}>
                    <NavLink to={item.to} end>
                      <item.icon aria-hidden="true" />
                      {label}
                    </NavLink>
                  </DropdownMenuItem>
                ) : (
                  <DropdownMenuItem key={item.key} disabled className={touch}>
                    <item.icon aria-hidden="true" />
                    {label}
                    <span className="ml-auto font-mono text-xs">{t.shell.soon}</span>
                  </DropdownMenuItem>
                )
              })}
              <DropdownMenuSeparator />
            </DropdownMenuGroup>
            <DropdownMenuItem
              aria-expanded={settingsOpen}
              className={touch}
              onSelect={(event) => {
                event.preventDefault() // keep the menu open while the section folds out
                setSettingsOpen((open) => !open)
              }}
            >
              <Settings aria-hidden="true" />
              {t.shell.settings}
              <ChevronDown
                aria-hidden="true"
                className={cn('ml-auto transition-transform motion-reduce:transition-none', settingsOpen && 'rotate-180')}
              />
            </DropdownMenuItem>
            {settingsOpen && (
              <>
                <DropdownMenuLabel inset className="font-normal">
                  {t.common.language}
                </DropdownMenuLabel>
                <DropdownMenuRadioGroup value={locale} onValueChange={(value) => setLocale(value as Locale)}>
                  {(Object.keys(LOCALE_NAMES) as Locale[]).map((option) => (
                    <DropdownMenuRadioItem key={option} value={option} lang={option} inset className={touch}>
                      {LOCALE_NAMES[option]}
                    </DropdownMenuRadioItem>
                  ))}
                </DropdownMenuRadioGroup>
              </>
            )}
            <DropdownMenuSeparator />
            <DropdownMenuItem onSelect={logout} className={touch}>
              <LogOut aria-hidden="true" />
              {t.shell.logOut}
            </DropdownMenuItem>
          </DropdownMenuContent>
        </DropdownMenu>
      </div>
    </header>
  )
}

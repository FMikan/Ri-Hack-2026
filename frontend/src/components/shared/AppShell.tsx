import {
  ChevronDown,
  GraduationCap,
  LayoutDashboard,
  LogOut,
  Menu,
  Settings,
  Store,
  Target,
  Trophy,
  UserRound,
  Users,
  type LucideIcon,
} from 'lucide-react'
import { useEffect, useState, type MouseEvent } from 'react'
import { NavLink, Outlet, useLocation } from 'react-router'
import coin from '@/assets/brand/stackoff-coin-on-dark.svg'
import coinSmall from '@/assets/brand/stackoff-coin-small-on-dark.svg'
import wordmark from '@/assets/brand/stackoff-wordmark-on-dark.svg'
import { UserAvatar } from '@/components/shared/UserAvatar'
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
import { useAuth } from '@/features/auth/auth-context'
import { PlayDialog } from '@/features/play/PlayDialog'
import { usePoll } from '@/hooks/use-poll'
import { api } from '@/lib/api-client'
import { LOCALE_NAMES, useLocale, type Locale } from '@/lib/i18n/locale-context'
import { cn } from '@/lib/utils'

type NavKey = 'dashboard' | 'practice' | 'professor' | 'leaderboards' | 'friends' | 'shop' | 'profile'

// Screens without a route yet show as "Soon"; give an item a `to` when its page lands.
const NAV: { key: NavKey; icon: LucideIcon; to?: string }[] = [
  { key: 'dashboard', icon: LayoutDashboard, to: '/' },
  { key: 'practice', icon: Target, to: '/practice' },
  { key: 'professor', icon: GraduationCap, to: '/professor' },
  { key: 'leaderboards', icon: Trophy, to: '/leaderboards' },
  { key: 'friends', icon: Users, to: '/friends' },
  { key: 'shop', icon: Store, to: '/shop' },
  { key: 'profile', icon: UserRound, to: '/profile' },
]

const focusRing = 'focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-ring'
const keepPolling = () => false

// An item stays lit on its sub-pages too (the professor's lessons and quizzes sit under /professor).
const isActive = (to: string, pathname: string) => (to === '/' ? pathname === '/' : pathname === to || pathname.startsWith(`${to}/`))

// A mouse click leaves focus on the link, which would keep the rail wide (focus-within) until a click elsewhere.
// Keyboard activation has detail 0 and keeps focus, so tabbing through the rail still works.
function releaseFocus(event: MouseEvent<HTMLElement>) {
  if (event.detail > 0) event.currentTarget.blur()
}

// Signed-in layout: icon rail on desktop, top bar with coins and the menu. On phones the menu carries the navigation.
export function AppShell() {
  return (
    <div className="min-h-svh lg:grid lg:grid-cols-[5rem_minmax(0,1fr)]">
      <SideRail />
      {/* A page marked data-fit-screen (the professor's chat) gets exactly the screen and scrolls inside itself. */}
      <div className="flex min-w-0 flex-col has-[[data-fit-screen]]:h-dvh">
        <TopBar />
        <Outlet />
      </div>
      <PlayDialog />
    </div>
  )
}

// Collapsed to icons. Hovering or tabbing into it widens it over the page and shows the full names.
function SideRail() {
  const { t } = useLocale()
  const { pathname } = useLocation()
  const reveal = 'opacity-0 transition-opacity group-focus-within/rail:opacity-100 group-hover/rail:opacity-100'
  const friends = usePoll(api.friends, 10_000, keepPolling)
  const { refresh } = friends
  // Refetch on navigation, so a request answered on Friends clears the dot as soon as you leave.
  useEffect(() => {
    refresh()
  }, [pathname, refresh])
  // Hidden on Friends itself: that page lists the requests.
  const requests = pathname === '/friends' ? 0 : (friends.data?.filter((friend) => friend.status === 'pending_in').length ?? 0)

  return (
    <aside className="group/rail sticky top-0 z-40 hidden h-svh lg:block">
      <div className="absolute inset-y-0 left-0 flex w-20 flex-col gap-8 overflow-hidden border-r border-border bg-sidebar py-5 shadow-black/50 transition-[width,box-shadow] duration-200 ease-out group-focus-within/rail:w-60 group-focus-within/rail:shadow-2xl group-hover/rail:w-60 group-hover/rail:shadow-2xl motion-reduce:transition-none">
        <NavLink
          to="/"
          aria-label="StackOff"
          onClick={releaseFocus}
          className={cn('relative mx-5 flex h-10 items-center rounded-sm', focusRing)}
        >
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
                  <span className="relative shrink-0">
                    <item.icon aria-hidden="true" className="size-5" />
                    {item.key === 'friends' && requests > 0 && (
                      <span aria-hidden="true" className="absolute -top-0.5 -right-0.5 size-2.5 rounded-full bg-destructive ring-2 ring-sidebar" />
                    )}
                  </span>
                  <span className={reveal}>{label}</span>
                  {item.key === 'friends' && requests > 0 && <span className="sr-only">{t.shell.friendRequests(requests)}</span>}
                  {!item.to && <span className={cn('ml-auto font-mono text-xs', reveal)}>{t.shell.soon}</span>}
                </>
              )
              return (
                <li key={item.key}>
                  {item.to ? (
                    <NavLink
                      to={item.to}
                      end={item.to === '/'}
                      onClick={releaseFocus}
                      className={cn(
                        base,
                        'transition-colors',
                        isActive(item.to, pathname)
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
                      onClick={releaseFocus}
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
  const user = session.user
  const touch = 'max-lg:py-3' // 44px rows on phones, where this menu is the navigation

  return (
    // Phones keep the bar pinned (logo, coins, menu); desktop has the rail for navigation.
    <header className="mx-auto flex w-full max-w-7xl items-center gap-3 px-4 py-3 max-lg:sticky max-lg:top-0 max-lg:z-30 max-lg:border-b max-lg:border-border max-lg:bg-background sm:px-6 lg:px-8 lg:py-5 cramped:py-1">
      <NavLink to="/" className={cn('rounded-sm lg:hidden', focusRing)}>
        <Wordmark className="h-6" />
      </NavLink>
      {pathname === '/' && (
        <p className="hidden min-w-0 truncate font-heading text-lg font-medium tracking-tight text-muted-foreground lg:block">
          {t.shell.greeting(user.username)}
        </p>
      )}
      <div className="ml-auto flex items-center gap-2 sm:gap-3">
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
            <UserAvatar user={user} className="hidden size-8 lg:block" />
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
                  <DropdownMenuItem key={item.key} asChild className={cn(touch, isActive(item.to, pathname) && 'text-primary')}>
                    <NavLink to={item.to} end={item.to === '/'}>
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
                {/* Language is a row like the others with a small EN | HR switch (the auth pages' look). Still radio
                    items, so arrow keys reach them; the menu stays open to show the switch. */}
                <div className="flex items-center gap-1.5 py-0.5 pr-1.5 pl-7 text-sm max-lg:py-1.5">
                  <span id="menu-language">{t.common.language}</span>
                  <DropdownMenuRadioGroup
                    aria-labelledby="menu-language"
                    value={locale}
                    // Radix reports a click on the checked item too: that one flips to the other language (a toggle).
                    onValueChange={(value) =>
                      setLocale(value === locale ? (Object.keys(LOCALE_NAMES) as Locale[]).find((option) => option !== locale)! : (value as Locale))
                    }
                    className="relative ml-auto flex gap-0.5 rounded-lg bg-secondary p-0.5"
                  >
                    {/* The thumb behind the picked language slides over (segments are equal, 2.5rem plus the gap). */}
                    <span
                      aria-hidden="true"
                      style={{ translate: `calc(${(Object.keys(LOCALE_NAMES) as Locale[]).indexOf(locale)} * (100% + 0.125rem))` }}
                      className="absolute inset-y-0.5 left-0.5 w-10 rounded-md bg-primary/15 ring-1 ring-primary/40 ring-inset transition-[translate] duration-200 ease-[cubic-bezier(0.2,0,0,1)] motion-reduce:transition-none"
                    />
                    {(Object.keys(LOCALE_NAMES) as Locale[]).map((option) => (
                      <DropdownMenuRadioItem
                        key={option}
                        value={option}
                        lang={option}
                        aria-label={LOCALE_NAMES[option]}
                        onSelect={(event) => event.preventDefault()}
                        className="relative h-7 w-10 justify-center px-0 font-mono text-xs font-medium text-muted-foreground uppercase transition-colors duration-200 focus:bg-foreground/5 focus:text-foreground data-[state=checked]:text-primary focus:data-[state=checked]:text-primary motion-reduce:transition-none *:data-[slot=dropdown-menu-radio-item-indicator]:hidden"
                      >
                        {option}
                      </DropdownMenuRadioItem>
                    ))}
                  </DropdownMenuRadioGroup>
                </div>
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

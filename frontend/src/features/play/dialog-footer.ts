// Pinned to the bottom of the scrolling dialog, so the main action stays in reach on phones. -bottom-4 cancels the
// overlay's p-4: sticky offsets count from inside the scroller's padding, and bottom-0 left a strip of scrolling content
// showing under the footer.
export const dialogFooter =
  'sticky -bottom-4 -mx-5 -mb-5 flex flex-wrap items-center justify-end gap-3 border-t border-border bg-card px-5 py-4 sm:-mx-6 sm:-mb-6 sm:px-6'

import { Icon as IconifyIcon } from "@iconify/react";
export function Iconify({ icon, width = 24, sx, ...props }) {
  return <IconifyIcon icon={icon} width={width} sx={sx} {...props} />;
}

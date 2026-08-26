import SimpleBar from "simplebar-react";
import "simplebar-react/dist/simplebar.min.css";

export function Scrollbar({ children, sx, ...props }) {
  return <SimpleBar style={{ maxHeight: "100%" }} sx={sx} {...props}>{children}</SimpleBar>;
}

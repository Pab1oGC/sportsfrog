import { useState, useCallback } from 'react';
import Box from '@mui/material/Box';
import Typography from '@mui/material/Typography';
import Button from '@mui/material/Button';
import Dialog from '@mui/material/Dialog';
import DialogTitle from '@mui/material/DialogTitle';
import DialogContent from '@mui/material/DialogContent';
import DialogActions from '@mui/material/DialogActions';
import TextField from '@mui/material/TextField';
import Alert from '@mui/material/Alert';
import CircularProgress from '@mui/material/CircularProgress';
import { Iconify } from 'src/components/iconify';

/**
 * Header de página reutilizable: título a la izquierda + acciones a la derecha.
 *
 * Antes cada página repetía:
 *   <Box sx={{ display: 'flex', justifyContent: 'space-between', mb: 3 }}>
 *     <Typography variant="h4" fontWeight={700}>Título</Typography>
 *     <Button>...</Button>
 *   </Box>
 */
export function PageHeader({ title, action, actionLabel, actionIcon, onAction, children }) {
  return (
    <Box sx={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', mb: 3, flexWrap: 'wrap', gap: 1 }}>
      <Typography variant="h4" fontWeight={700}>{title}</Typography>
      <Box sx={{ display: 'flex', gap: 1, alignItems: 'center' }}>
        {children}
        {action && (
          <Button variant="contained" startIcon={<Iconify icon={actionIcon || 'eva:plus-fill'} />} onClick={onAction || action}>
            {actionLabel}
          </Button>
        )}
      </Box>
    </Box>
  );
}
namespace AbilityKit.Game.Flow
{
    internal sealed class BattleHudInputState
    {
        private float _moveDx;
        private float _moveDz;
        private bool _hasMove;

        private int _skillClickSlot;

        private bool _skillAiming;
        private int _skillAimSlot;
        private float _skillAimDx;
        private float _skillAimDz;
        private bool _skillAimPreviewSubmitted;
        private int _skillAimPreviewSubmissionVersion;

        private bool _skillAimSubmit;
        private int _skillAimSubmitSlot;
        private float _skillAimSubmitPosX;
        private float _skillAimSubmitPosY;
        private float _skillAimSubmitPosZ;
        private float _skillAimSubmitDirX;
        private float _skillAimSubmitDirY;
        private float _skillAimSubmitDirZ;

        public bool TryReadMove(out float dx, out float dz)
        {
            if (_hasMove)
            {
                dx = _moveDx;
                dz = _moveDz;
                return true;
            }

            dx = 0f;
            dz = 0f;
            return false;
        }

        public bool TryConsumeSkillClick(out int slot)
        {
            slot = _skillClickSlot;
            if (slot <= 0) return false;

            _skillClickSlot = 0;
            return true;
        }

        public bool TryConsumeSkillAimSubmit(
            out int slot,
            out float aimPosX,
            out float aimPosY,
            out float aimPosZ,
            out float aimDirX,
            out float aimDirY,
            out float aimDirZ)
        {
            if (_skillAimSubmit && _skillAimSubmitSlot > 0)
            {
                slot = _skillAimSubmitSlot;
                aimPosX = _skillAimSubmitPosX;
                aimPosY = _skillAimSubmitPosY;
                aimPosZ = _skillAimSubmitPosZ;
                aimDirX = _skillAimSubmitDirX;
                aimDirY = _skillAimSubmitDirY;
                aimDirZ = _skillAimSubmitDirZ;

                _skillAimSubmit = false;
                _skillAimSubmitSlot = 0;
                _skillAimSubmitPosX = 0f;
                _skillAimSubmitPosY = 0f;
                _skillAimSubmitPosZ = 0f;
                _skillAimSubmitDirX = 0f;
                _skillAimSubmitDirY = 0f;
                _skillAimSubmitDirZ = 0f;
                return true;
            }

            slot = 0;
            aimPosX = 0f;
            aimPosY = 0f;
            aimPosZ = 0f;
            aimDirX = 0f;
            aimDirY = 0f;
            aimDirZ = 0f;
            return false;
        }

        public void BeginMove()
        {
            _hasMove = true;
        }

        public void EndMove()
        {
            _hasMove = false;
            _moveDx = 0f;
            _moveDz = 0f;
        }

        public void SetMove(float dx, float dz)
        {
            _moveDx = dx;
            _moveDz = dz;
        }

        public void SubmitSkillClick(int slot)
        {
            _skillClickSlot = slot;
        }

        public void SetSkillAim(int slot, float dx, float dz, bool aiming)
        {
            _skillAiming = aiming;
            _skillAimSlot = slot;
            _skillAimDx = dx;
            _skillAimDz = dz;
            if (aiming)
            {
                _skillAimPreviewSubmitted = false;
            }
        }

        public void CancelSkillAim()
        {
            _skillAiming = false;
            _skillAimSlot = 0;
            _skillAimDx = 0f;
            _skillAimDz = 0f;
            _skillAimPreviewSubmitted = false;
        }

        public void SubmitSkillAim(
            int slot,
            float aimDx,
            float aimDz,
            float aimPosX,
            float aimPosY,
            float aimPosZ,
            float aimDirX,
            float aimDirY,
            float aimDirZ)
        {
            _skillAiming = false;
            _skillAimSlot = slot;
            _skillAimDx = aimDx;
            _skillAimDz = aimDz;
            _skillAimPreviewSubmitted = true;
            _skillAimPreviewSubmissionVersion++;
            _skillAimSubmit = true;
            _skillAimSubmitSlot = slot;
            _skillAimSubmitPosX = aimPosX;
            _skillAimSubmitPosY = aimPosY;
            _skillAimSubmitPosZ = aimPosZ;
            _skillAimSubmitDirX = aimDirX;
            _skillAimSubmitDirY = aimDirY;
            _skillAimSubmitDirZ = aimDirZ;
        }

        public bool TryReadSkillAimPreview(out int slot, out float dx, out float dz, out int submissionVersion)
        {
            if (_skillAiming || _skillAimPreviewSubmitted)
            {
                slot = _skillAimSlot;
                dx = _skillAimDx;
                dz = _skillAimDz;
                submissionVersion = _skillAimPreviewSubmitted ? _skillAimPreviewSubmissionVersion : 0;
                return slot > 0;
            }

            slot = 0;
            dx = 0f;
            dz = 0f;
            submissionVersion = 0;
            return false;
        }

        public void Reset()
        {
            _moveDx = 0f;
            _moveDz = 0f;
            _hasMove = false;
            _skillClickSlot = 0;

            _skillAiming = false;
            _skillAimSlot = 0;
            _skillAimDx = 0f;
            _skillAimDz = 0f;
            _skillAimPreviewSubmitted = false;
            _skillAimPreviewSubmissionVersion = 0;

            _skillAimSubmit = false;
            _skillAimSubmitSlot = 0;
            _skillAimSubmitPosX = 0f;
            _skillAimSubmitPosY = 0f;
            _skillAimSubmitPosZ = 0f;
            _skillAimSubmitDirX = 0f;
            _skillAimSubmitDirY = 0f;
            _skillAimSubmitDirZ = 0f;
        }
    }
}

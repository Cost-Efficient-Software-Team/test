﻿namespace SelfGuidedTours.Core.Models.Dto
{
    public class UserProfileDto
    {
        public Guid UserId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
    }
}

﻿using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;

namespace SelfGuidedTours.Infrastructure.Data.Models
{
    public class ApplicationUser : IdentityUser
    {
        public string Name { get; set; } = null!;
        [Comment("Id of the stripe customer associated with the user. Created when the user makes a payment.")]
        public string? StripeCustomerId { get; set; }
        public string? Credentials { get; set; }
        [Required]
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public virtual ICollection<Tour> CreatedTours { get; set; } = new HashSet<Tour>();
        public virtual ICollection<Payment> Payments { get; set; } = new HashSet<Payment>();
        public virtual ICollection<Review> Reviews { get; set; } = new HashSet<Review>();
        public virtual ICollection<UserTours> UserTours { get; set; } = new HashSet<UserTours>();
        public Wallet Wallet { get; set; } = null!;
        public DateTime? UpdatedAt { get; set;}
    }
}

﻿using AutoMapper;
using HotelManagementSystem.Interfaces.Dto;
using HotelManagementSystem.Interfaces.Exceptions;
using HotelManagementSystem.Interfaces.Services;
using HotelManagementSystem.Services.Repositories;
using Microsoft.EntityFrameworkCore;

namespace HotelManagementSystem.Services
{
    public class ReservationService(HotelScope hotelScope, IMapper mapper) : IReservationService
    {
        private readonly HotelScope _hotelScope = hotelScope;
        private readonly IMapper _mapper = mapper;

        public async Task<Reservation> CreateAsync(Reservation reservation)
        {
            await Validate(reservation);

            var reservationEntity = _mapper.Map<Interfaces.Entities.Reservation>(reservation);

            reservationEntity.CheckInDate = reservation.CheckInDate.ToUniversalTime();
            reservationEntity.CheckOutDate = reservation.CheckOutDate.ToUniversalTime();

            var reservationCreated = await _hotelScope.DbContext.Reservations.AddAsync(reservationEntity);

            if (reservationCreated.Entity is null)
            {
                throw new Exception("Could not create reservation.");
            }

            await _hotelScope.DbContext.SaveChangesAsync();

            reservation.Id = reservationCreated.Entity.Id;

            return reservation;
        }

        public async Task<Reservation> GetReservationAsync(int reservationId)
        {
            var reservation = _mapper.Map<Reservation>(await _hotelScope.DbContext.Reservations
                .AsNoTracking()
                .Where(h => h.Id == reservationId)
                .SingleOrDefaultAsync());

            if (reservation is null)
            {
                throw new NotFoundException($"Reservation {reservationId} could not be found");
            }

            return reservation;
        }

        public async Task<IEnumerable<Reservation>> GetReservationsAsync()
        {
            return (await _hotelScope.DbContext.Reservations
                .AsNoTracking()
                .ToListAsync())
                .Select(_mapper.Map<Reservation>);
        }

        public async Task DeleteAsync(int reservationId)
        {
            var reservation = await _hotelScope.DbContext.Reservations
                .Where(h => h.Id == reservationId)
                .SingleOrDefaultAsync();

            if (reservation is null)
            {
                throw new NotFoundException($"Reservation {reservationId} could not be found");
            }

            _hotelScope.DbContext.Reservations.Remove(reservation);

            await _hotelScope.DbContext.SaveChangesAsync();
        }

        public async Task<Reservation> UpdateAsync(Reservation reservation)
        {
            await Validate(reservation);

            var reservationUpdated = _hotelScope.DbContext.Reservations.Update(_mapper.Map<Interfaces.Entities.Reservation>(reservation));

            await _hotelScope.DbContext.SaveChangesAsync();

            return _mapper.Map<Reservation>(reservationUpdated.Entity);
        }

        private async Task Validate(Reservation reservation)
        {
            if (reservation is null)
            {
                throw new ArgumentNullException(nameof(reservation));
            }

            if (reservation.CheckOutDate <= reservation.CheckInDate)
            {
                throw new ValidationException($"Check out date {reservation.CheckOutDate:yyyy-MM-dd} can not be before or on the same day as the check in date {reservation.CheckInDate:yyyy-MM-dd}");
            }

            if (reservation.GuestCount < 1)
            {
                throw new ValidationException($"Guest count {reservation.GuestCount} can not be lower than 1");
            }

            var room = await _hotelScope.DbContext.Rooms
                .Where(r => r.Id == reservation.RoomId)
                .SingleOrDefaultAsync();

            if (room is null)
            {
                throw new NotFoundException($"Room {reservation.RoomId} could not be found");
            }

            if (room.Capacity < reservation.GuestCount)
            {
                throw new ValidationException($"Guest count {reservation.GuestCount} exceeds the rooms capacity {room.Capacity}.");
            }

            var reservationOverlaps = await _hotelScope.DbContext.Reservations
                .Where(r => r.RoomId == reservation.RoomId &&
                    r.CheckInDate < reservation.CheckOutDate &&
                    r.CheckOutDate > reservation.CheckInDate
                )
                .AnyAsync();

            if (reservationOverlaps)
            {
                throw new ValidationException($"Reservation with check in {reservation.CheckInDate:yyyy-MM-dd} and check out {reservation.CheckOutDate:yyyy-MM-dd} dates intersect with an already existing reservations.");
            }
        }
    }
}

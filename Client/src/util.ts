export enum Users { Chris = 'Chris', Courtney = 'Courtney' };
export enum Integrations { Spotify = 'Spotify', ToDo = 'ToDo' };

export function random(length: number = 8) {
    // Declare all characters
    let chars = 'ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789';

    // Pick characers randomly
    let str = '';
    for (let i = 0; i < length; i++) {
        str += chars.charAt(Math.floor(Math.random() * chars.length));
    }

    return str;
}
